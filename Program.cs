using stdiscm_PS3.Services;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ConsumerBackend.Models;
using System.Threading.Channels;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var cfg = builder.Configuration;
int grpcPort = cfg.GetValue<int>("GrpcPort", 5001);
int httpPort = cfg.GetValue<int>("HttpPort", 5000);
int queueCapacity = cfg.GetValue<int>("UploadQueueCapacity", 10);
int workerCount = cfg.GetValue<int>("UploadWorkerCount", 4);
string uploadFolder = cfg.GetValue<string>("uploadFolder") ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");
string httpBaseUrl = cfg.GetValue<string>("httpBaseUrl") ?? $"http://localhost:{httpPort}";

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Add services to the container.
builder.Services.AddGrpc();

var bucketTokens = new SemaphoreSlim(queueCapacity, queueCapacity);
builder.Services.AddSingleton(bucketTokens);

// Job channel (unbounded) and a map to track job completion TCS
var jobChannel = Channel.CreateUnbounded<UploadJob>(new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });
builder.Services.AddSingleton(jobChannel);
var jobCompletionMap = new ConcurrentDictionary<string, TaskCompletionSource<(bool, string, string)>>();
builder.Services.AddSingleton(jobCompletionMap);

// Provide uploadFolder/httpBaseUrl to services via DI
builder.Services.AddSingleton(uploadFolder);
builder.Services.AddSingleton(httpBaseUrl);

builder.Services.AddSingleton<VideoUploadServiceImpl>();
builder.Services.AddSingleton<VideoLibraryServiceImpl>();

var app = builder.Build();

Directory.CreateDirectory(uploadFolder);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadFolder),
    RequestPath = "/media",
    ServeUnknownFileTypes = true // to serve files without known MIME types
});

// Configure the HTTP request pipeline.
app.MapGrpcService<GreeterService>();
// Map gRPC services
app.MapGrpcService<VideoUploadServiceImpl>();
app.MapGrpcService<VideoLibraryServiceImpl>();

// Start worker tasks
var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
var logger = loggerFactory.CreateLogger("WorkerPool");

// Worker method
async Task WorkerLoopAsync(int workerIndex, CancellationToken stoppingToken)
{
    var reader = jobChannel.Reader;
    while (await reader.WaitToReadAsync(stoppingToken))
    {
        while (reader.TryRead(out var job))
        {
            var workerLogger = loggerFactory.CreateLogger($"Worker-{workerIndex}");
            try
            {
                workerLogger.LogInformation("Worker {w} processing job {jobId} file {file}.", workerIndex, job.JobId, job.OriginalFileName);
                // verify checksum if provided (optional)
                bool checksumOk = true;
                if (!string.IsNullOrEmpty(job.ChecksumSha256))
                {
                    try
                    {
                        using var sha = SHA256.Create();
                        using var fs = File.OpenRead(job.TempFilePath);
                        var hash = await sha.ComputeHashAsync(fs, stoppingToken);
                        var hex = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                        checksumOk = string.Equals(hex, job.ChecksumSha256.Replace("-", "").ToLowerInvariant(), StringComparison.OrdinalIgnoreCase);
                        if (!checksumOk)
                        {
                            workerLogger.LogWarning("Checksum mismatch for job {jobId}. Expected {exp}, actual {act}", job.JobId, job.ChecksumSha256, hex);
                        }
                    }
                    catch (Exception ex)
                    {
                        workerLogger.LogError(ex, "Checksum compute fail for job {jobId}", job.JobId);
                        checksumOk = false;
                    }
                }

                // move temp file to final name
                var safeFileName = Path.GetFileName(job.OriginalFileName);
                var finalFileName = $"{job.JobId}_{safeFileName}";
                var finalPath = Path.Combine(uploadFolder, finalFileName);

                // If final exists (unlikely), append suffix
                if (File.Exists(finalPath))
                {
                    finalFileName = $"{job.JobId}_{Guid.NewGuid():N}_{safeFileName}";
                    finalPath = Path.Combine(uploadFolder, finalFileName);
                }

                File.Move(job.TempFilePath, finalPath); // atomic on same volume

                var playbackUrl = $"{httpBaseUrl}/media/{Uri.EscapeDataString(finalFileName)}";

                // Set job completion TCS if any
                if (jobCompletionMap.TryRemove(job.JobId, out var tcs))
                {
                    tcs.TrySetResult((checksumOk, checksumOk ? "Uploaded" : "Uploaded (checksum mismatch)", playbackUrl));
                }

                workerLogger.LogInformation("Worker {w} finished job {jobId}. saved as {final}", workerIndex, job.JobId, finalFileName);
            }
            catch (Exception ex)
            {
                if (jobCompletionMap.TryRemove(job.JobId, out var tcs))
                {
                    tcs.TrySetResult((false, $"Processing error: {ex.Message}", string.Empty));
                }
                logger.LogError(ex, "Error processing job {jobId}", job.JobId);
            }
            finally
            {
                // release token so another upload can be accepted
                try { bucketTokens.Release(); } catch { }
            }
        }
    }
}

// Start N workers
var cts = new CancellationTokenSource();
for (int i = 0; i < workerCount; i++)
{
    var idx = i;
    _ = Task.Run(() => WorkerLoopAsync(idx + 1, cts.Token));
}

app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");
app.Urls.Add($"http://0.0.0.0:{httpPort}");
app.Urls.Add($"http://0.0.0.0:{grpcPort}"); // note: plaintext gRPC on HTTP/1.1 is possible if client supports; for gRPC-Web you may want to enable gRPC-Web.
app.Run();
