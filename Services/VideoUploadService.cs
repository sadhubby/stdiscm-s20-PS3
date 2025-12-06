using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using ConsumerBackend.Models;
using System.Text;
using System.Security.Cryptography;
using System.Threading.Channels;
using System.Collections.Concurrent;
using stdiscm_PS3;

namespace stdiscm_PS3.Services
{
       public class VideoUploadServiceImpl : VideoUploadService.VideoUploadServiceBase
    {
        private readonly ILogger<VideoUploadServiceImpl> _logger;
        private readonly string _uploadFolder;
        private readonly SemaphoreSlim _bucketTokens; // leaky-bucket tokens
        private readonly Channel<UploadJob> _jobChannel;
        private readonly ConcurrentDictionary<string, TaskCompletionSource<(bool success, string message, string playbackUrl)>> _jobCompletionMap;
        private readonly string _httpBaseUrl; // used to build playback URL

        public VideoUploadServiceImpl(
            ILogger<VideoUploadServiceImpl> logger,
            SemaphoreSlim bucketTokens,
            Channel<UploadJob> jobChannel,
            ConcurrentDictionary<string, TaskCompletionSource<(bool, string, string)>> jobCompletionMap,
            string uploadFolder,
            string httpBaseUrl)
        {
            _logger = logger;
            _bucketTokens = bucketTokens;
            _jobChannel = jobChannel;
            _jobCompletionMap = jobCompletionMap;
            _uploadFolder = uploadFolder;
            _httpBaseUrl = httpBaseUrl.TrimEnd('/');
        }

        public override async Task<VideoUploadResponse> UploadVideo(IAsyncStreamReader<VideoUploadRequest> requestStream, ServerCallContext context)
        {
            VideoMetadata? metadata = null;
            bool accepted = false;
            string tempFilePath = null!;
            var tcs = new TaskCompletionSource<(bool, string, string)>(TaskCreationOptions.RunContinuationsAsynchronously);
            UploadJob? job = null;

            try
            {
                // First read until we get metadata. We will wait for metadata message as first meaningful one.
                // We'll handle streaming generically: if metadata arrives, we attempt to reserve a bucket token.
                // If token reserved => accept; else mark rejected and drain incoming chunks.

                // Wait for first messages and react accordingly
                while (await requestStream.MoveNext())
                {
                    var req = requestStream.Current;
                    if (req.RequestCase == VideoUploadRequest.RequestOneofCase.Metadata)
                    {
                        metadata = req.Metadata;

                        // Try to reserve token (non-blocking). If no tokens -> drop
                        if (await _bucketTokens.WaitAsync(0))
                        {
                            accepted = true;
                            _logger.LogInformation("Accepted upload from producer {producer} for file {file}. Token reserved.", metadata.ProducerId, metadata.FileName);
                            // prepare temp file
                            Directory.CreateDirectory(_uploadFolder);
                            tempFilePath = Path.Combine(_uploadFolder, $"tmp_{Guid.NewGuid():N}.part");
                            break; // exit to start writing chunks
                        }
                        else
                        {
                            // No capacity in leaky bucket: drop upload. We still must drain the incoming stream to consume it.
                            _logger.LogWarning("Dropping upload from producer {producer} for file {file}: queue full.", metadata.ProducerId, metadata.FileName);
                            // drain rest of stream and return failure.
                            await DrainStreamAsync(requestStream);
                            return new VideoUploadResponse
                            {
                                Success = false,
                                Message = "Queue is full. Upload dropped."
                            };
                        }
                    }
                    else if (req.RequestCase == VideoUploadRequest.RequestOneofCase.Chunk)
                    {
                        // If we haven't seen metadata yet but got chunk, we will ignore until metadata. Continue loop.
                        _logger.LogDebug("Received chunk before metadata; ignoring until metadata is sent.");
                    }
                }

                if (!accepted)
                {
                    // If stream ended before metadata or we never accepted but finished reading
                    return new VideoUploadResponse
                    {
                        Success = false,
                        Message = "No metadata provided or upload not accepted."
                    };
                }

                // Now we have accepted and have tempFilePath and must write the remainder of the stream (including any chunk messages already read? We broke out, so continue reading)
                long totalBytes = 0;
                using (var fs = new FileStream(tempFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, true))
                {
                    // write any remaining messages
                    while (await requestStream.MoveNext())
                    {
                        var req = requestStream.Current;
                        if (req.RequestCase == VideoUploadRequest.RequestOneofCase.Chunk)
                        {
                            var bytes = req.Chunk.ToByteArray();
                            await fs.WriteAsync(bytes, 0, bytes.Length, context.CancellationToken);
                            totalBytes += bytes.Length;
                        }
                        else if (req.RequestCase == VideoUploadRequest.RequestOneofCase.Metadata)
                        {
                            // ignore duplicate metadata messages
                        }
                    }

                    await fs.FlushAsync();
                }

                // Create job and enqueue
                job = new UploadJob
                {
                    TempFilePath = tempFilePath,
                    OriginalFileName = metadata!.FileName,
                    ChecksumSha256 = metadata.ChecksumSha256,
                    ProducerId = metadata.ProducerId,
                    SizeBytes = totalBytes,
                    ReceivedAtUtc = DateTimeOffset.UtcNow
                };

                // register completion TCS to map so worker can set final URL and success
                _jobCompletionMap.TryAdd(job.JobId, tcs);

                // set a field on job to let worker find the tcs via map key: we use JobId as the key
                // (job.JobId already set by constructor)

                // Enqueue job into internal channel (unbounded); workers will process
                var wrote = await _jobChannel.Writer.WaitToWriteAsync(context.CancellationToken);
                if (!wrote)
                {
                    // channel closed unexpectedly
                    _jobCompletionMap.TryRemove(job.JobId, out _);
                    File.Delete(tempFilePath);
                    _bucketTokens.Release(); // free reserved token
                    return new VideoUploadResponse
                    {
                        Success = false,
                        Message = "Server not accepting jobs (shutting down)."
                    };
                }

                if (!_jobChannel.Writer.TryWrite(job))
                {
                    // fallback: shouldn't happen if WaitToWriteAsync succeeded
                    _jobCompletionMap.TryRemove(job.JobId, out _);
                    File.Delete(tempFilePath);
                    _bucketTokens.Release(); // free reserved token
                    return new VideoUploadResponse
                    {
                        Success = false,
                        Message = "Failed to enqueue job."
                    };
                }

                // Wait for processing completion (worker will set tcs result)
                // You may choose a timeout here to avoid holding connection forever
                var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(30), context.CancellationToken));
                if (completed != tcs.Task)
                {
                    // timeout
                    // We won't delete files here — worker will finish eventually and release token.
                    return new VideoUploadResponse
                    {
                        Success = true,
                        Message = "Upload accepted; processing in background.",
                        VideoId = job.JobId
                    };
                }

                var (success, message, playbackUrl) = await tcs.Task;
                return new VideoUploadResponse
                {
                    Success = success,
                    Message = message,
                    VideoId = job.JobId,
                    PlaybackUrl = playbackUrl
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UploadVideo");
                // cleanup
                if (job != null)
                {
                    _jobCompletionMap.TryRemove(job.JobId, out _);
                }
                if (!string.IsNullOrEmpty(tempFilePath) && File.Exists(tempFilePath))
                {
                    try { File.Delete(tempFilePath); } catch { }
                }
                if (accepted)
                {
                    // release reserved token if we reserved one and job didn't get processed
                    try { _bucketTokens.Release(); } catch { }
                }
                return new VideoUploadResponse
                {
                    Success = false,
                    Message = $"Server error: {ex.Message}"
                };
            }
        }

        private async Task DrainStreamAsync(IAsyncStreamReader<VideoUploadRequest> stream)
        {
            try
            {
                while (await stream.MoveNext())
                {
                    // just discard
                }
            }
            catch { /* ignore */ }
        }
    }
};
