using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using stdiscm_PS3;

namespace ProducerApp
{
    class Program
    {
        // Allowed video extensions (lowercase, without dot)
        static readonly string[] AllowedExtensions = new[] { "mp4", "mkv", "mov", "avi", "wmv", "webm", "mpeg", "mpg", "m4v" };

        static async Task<int> Main(string[] args)
        {
            Console.WriteLine("Producer starting...");

            // Defaults
            int producers = 1;
            int maxQueue = 10; // unused locally but shown to user
            int consumers = 1; // informational
            string baseFolder = Path.Combine(Directory.GetCurrentDirectory(), "producer_inputs");
            string serverAddress = "http://localhost:5000";
            int chunkSize = 64 * 1024; // 64KB
            int maxRetries = 5;

            // If args provided, run non-interactive mode: p baseFolder serverAddress
            if (args.Length >= 1) int.TryParse(args[0], out producers);
            if (args.Length >= 2) baseFolder = args[1];
            if (args.Length >= 3) serverAddress = args[2];

            if (args.Length == 0)
            {
                // Interactive-first flow
                Console.WriteLine("Interactive producer setup (press Enter to accept defaults shown)");

                producers = PromptInt("Number of producers (p)", producers);
                consumers = PromptInt("(Informational) Number of consumers (c)", consumers);
                maxQueue = PromptInt("(Informational) Max queue length (q)", maxQueue);

                baseFolder = PromptString("Base folder for producer inputs", baseFolder);
                if (!Directory.Exists(baseFolder))
                {
                    Console.WriteLine($"Folder '{baseFolder}' does not exist.");
                    var create = PromptString("Create it? (y/N)", "N");
                    if (create.Trim().Equals("y", StringComparison.OrdinalIgnoreCase))
                    {
                        Directory.CreateDirectory(baseFolder);
                        Console.WriteLine("Created folder.");
                    }
                    else
                    {
                        Console.WriteLine("Please create the folder and re-run, or provide another path.");
                        return 1;
                    }
                }

                serverAddress = PromptString("Consumer server address (http://... or https://...)", serverAddress);
                if (!serverAddress.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !serverAddress.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Server address must start with http:// or https://");
                    return 1;
                }

                Console.WriteLine();
                Console.WriteLine("Configuration summary:");
                Console.WriteLine($"  producers = {producers}");
                Console.WriteLine($"  baseFolder = {baseFolder}");
                Console.WriteLine($"  server = {serverAddress}");
                Console.WriteLine($"  allowed extensions = {string.Join(",", AllowedExtensions)}");
                Console.WriteLine();

                var proceed = PromptString("Type 'start' to begin producers, or anything else to cancel", "");
                if (!proceed.Equals("start", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Cancelled by user.");
                    return 0;
                }
            }

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };

            // Ensure per-producer folders exist
            for (int i = 1; i <= producers; i++)
            {
                var pf = Path.Combine(baseFolder, $"producer{i}");
                Directory.CreateDirectory(pf);
            }

            var tasks = Enumerable.Range(1, producers)
                .Select(i => RunProducerAsync(i, Path.Combine(baseFolder, $"producer{i}"), serverAddress, chunkSize, maxRetries, cts.Token))
                .ToArray();

            Console.WriteLine("Press Ctrl+C to stop producers.");
            await Task.WhenAll(tasks);
            Console.WriteLine("All producers stopped.");
            return 0;
        }

        static int PromptInt(string prompt, int defaultValue)
        {
            Console.Write($"{prompt} [{defaultValue}]: ");
            var line = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(line)) return defaultValue;
            if (int.TryParse(line.Trim(), out var v)) return v;
            Console.WriteLine("Invalid integer, using default.");
            return defaultValue;
        }

        static string PromptString(string prompt, string defaultValue)
        {
            Console.Write($"{prompt} [{defaultValue}]: ");
            var line = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(line)) return defaultValue;
            return line.Trim();
        }

        static async Task RunProducerAsync(int id, string folder, string serverAddress, int chunkSize, int maxRetries, CancellationToken token)
        {
            Console.WriteLine($"Producer#{id} watching folder: {folder}");

            // Configure HTTP handler
            var handler = new HttpClientHandler();
            if (serverAddress.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                handler.ServerCertificateCustomValidationCallback = (msg, cert, chain, errors) => true; // For testing only!
            }

            var channel = GrpcChannel.ForAddress(serverAddress, new GrpcChannelOptions { HttpHandler = handler });
            var client = new VideoUploadService.VideoUploadServiceClient(channel);

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var files = Directory.EnumerateFiles(folder)
                        .Where(f => !f.EndsWith(".part", StringComparison.OrdinalIgnoreCase) && !f.EndsWith(".uploaded", StringComparison.OrdinalIgnoreCase))
                        .Where(f => AllowedExtensions.Contains(Path.GetExtension(f).TrimStart('.').ToLowerInvariant()))
                        .OrderBy(f => File.GetCreationTimeUtc(f))
                        .ToArray();

                    if (files.Length == 0)
                    {
                        await Task.Delay(1500, token);
                        continue;
                    }

                    foreach (var file in files)
                    {
                        if (token.IsCancellationRequested) break;
                        Console.WriteLine($"Producer#{id} uploading: {Path.GetFileName(file)}");
                        var ok = await UploadFileWithRetries(client, file, chunkSize, maxRetries, token);
                        if (ok)
                        {
                            Console.WriteLine($"Producer#{id} uploaded: {Path.GetFileName(file)}");
                            var dest = file + ".uploaded";
                            try { File.Move(file, dest, overwrite: true); } catch { }
                        }
                        else
                        {
                            Console.WriteLine($"Producer#{id} failed to upload after retries: {Path.GetFileName(file)}");
                        }
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Console.WriteLine($"Producer#{id} error: {ex.Message}");
                    await Task.Delay(2000, token);
                }
            }
        }

        static async Task<bool> UploadFileWithRetries(VideoUploadService.VideoUploadServiceClient client, string filePath, int chunkSize, int maxRetries, CancellationToken token)
        {
            int attempt = 0;
            var fileName = Path.GetFileName(filePath);
            var fileSize = new FileInfo(filePath).Length;
            var checksum = await ComputeSha256(filePath, token);

            // Use a single UploadId for all retries of the same file so retries are idempotent
            var uploadId = Guid.NewGuid().ToString();

            while (attempt < maxRetries && !token.IsCancellationRequested)
            {
                attempt++;
                try
                {
                    using var call = client.UploadVideo();

                    var metadata = new VideoMetadata
                    {
                        FileName = fileName,
                        FileType = Path.GetExtension(fileName).TrimStart('.'),
                        FileSizeBytes = fileSize,
                        UploadId = uploadId,
                        ChecksumSha256 = checksum,
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    };

                    var metaReq = new VideoUploadRequest { Metadata = metadata };
                    await call.RequestStream.WriteAsync(metaReq);

                    using var fs = File.OpenRead(filePath);
                    var buffer = new byte[chunkSize];
                    int read;
                    while ((read = await fs.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                    {
                        var chunkReq = new VideoUploadRequest { Chunk = ByteString.CopyFrom(buffer, 0, read) };
                        await call.RequestStream.WriteAsync(chunkReq);
                    }

                    await call.RequestStream.CompleteAsync();

                    var response = await call.ResponseAsync;
                    if (response != null && response.Success)
                    {
                        return true;
                    }

                    var msg = response?.Message ?? string.Empty;
                    Console.WriteLine($"Upload response: success={response?.Success}, message={msg}");
                    if (msg != null && msg.IndexOf("full", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var backoff = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                        Console.WriteLine($"Queue full, retrying after {backoff.TotalSeconds}s (attempt {attempt}/{maxRetries})");
                        await Task.Delay(backoff, token);
                        continue;
                    }

                    Console.WriteLine($"Upload failed: {msg}");
                    return false;
                }
                catch (RpcException rex) when (rex.StatusCode == StatusCode.ResourceExhausted || rex.StatusCode == StatusCode.Unavailable)
                {
                    var backoff = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    Console.WriteLine($"RPC indicates resource/exhausted/unavailable. Backing off {backoff.TotalSeconds}s (attempt {attempt}/{maxRetries})");
                    await Task.Delay(backoff, token);
                    continue;
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Console.WriteLine($"Upload exception: {ex.Message}");
                    return false;
                }
            }

            return false;
        }

        static async Task<string> ComputeSha256(string filePath, CancellationToken token)
        {
            using var sha = SHA256.Create();
            using var fs = File.OpenRead(filePath);
            var hash = await sha.ComputeHashAsync(fs, token);
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
