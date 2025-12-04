using Grpc.Core;
using stdiscm_PS3;
using System.Collections.Concurrent;

namespace ProducerTest;

/// <summary>
/// Mock VideoUploadService for testing Producer uploads.
/// Accepts uploads, persists them to disk under ProducerTest/uploads, and simulates transient queue-full responses.
/// The mock will fail the first attempt for a given UploadId and accept subsequent retries.
/// </summary>
public class MockVideoUploadService : VideoUploadService.VideoUploadServiceBase
{
    private readonly ILogger<MockVideoUploadService> _logger;
    private static readonly ConcurrentDictionary<string, int> _attempts = new();

    public MockVideoUploadService(ILogger<MockVideoUploadService> logger)
    {
        _logger = logger;
    }

    public override async Task<VideoUploadResponse> UploadVideo(
        IAsyncStreamReader<VideoUploadRequest> requestStream,
        ServerCallContext context)
    {
        VideoMetadata? metadata = null;
        long totalBytes = 0;
        int chunkCount = 0;

        // Prepare uploads folder
        var uploadsDir = Path.Combine(AppContext.BaseDirectory ?? Environment.CurrentDirectory, "uploads");
        try { Directory.CreateDirectory(uploadsDir); } catch { }

        string? tempFilePath = null;

        try
        {
            await foreach (var req in requestStream.ReadAllAsync())
            {
                if (req.RequestCase == VideoUploadRequest.RequestOneofCase.Metadata)
                {
                    metadata = req.Metadata;
                    _logger.LogInformation($"[MockServer] Upload started: {metadata.FileName} (ID: {metadata.UploadId})");
                    // create a temp file to append chunks
                    tempFilePath = Path.Combine(uploadsDir, $"{metadata.UploadId}_{Path.GetFileName(metadata.FileName)}.part");
                    try { using var _ = File.Create(tempFilePath); } catch { }
                }
                else if (req.RequestCase == VideoUploadRequest.RequestOneofCase.Chunk)
                {
                    if (tempFilePath == null)
                    {
                        // No metadata yet, ignore
                        continue;
                    }
                    var bytes = req.Chunk.ToByteArray();
                    await File.AppendAllBytesAsync(tempFilePath, bytes);
                    totalBytes += bytes.Length;
                    chunkCount++;
                    _logger.LogDebug($"[MockServer] Chunk {chunkCount}: {bytes.Length} bytes");
                }
            }

            if (metadata == null)
            {
                return new VideoUploadResponse
                {
                    Success = false,
                    Message = "No metadata received"
                };
            }

            _logger.LogInformation($"[MockServer] Upload complete: {metadata.FileName}, {totalBytes} bytes in {chunkCount} chunks");

            // Decide whether to simulate queue-full: fail only on the first attempt per UploadId
            var uploadId = metadata.UploadId;
            if (string.IsNullOrEmpty(uploadId)) uploadId = Guid.NewGuid().ToString();
            var attempt = _attempts.GetOrAdd(uploadId, 0);
            if (attempt == 0)
            {
                // mark that we've seen first attempt (thread-safe)
                _attempts.AddOrUpdate(uploadId, 1, (k, v) => v + 1);
                _logger.LogWarning("[MockServer] Simulating queue full for first attempt of UploadId {id}", uploadId);

                // Keep persisted partial file (so retries don't lose data) and respond with queue-full
                return new VideoUploadResponse
                {
                    Success = false,
                    Message = "Queue is full, please retry later"
                };
            }

            // On success, rename .part to final file name (remove .part suffix)
            if (!string.IsNullOrEmpty(tempFilePath) && File.Exists(tempFilePath))
            {
                var finalPath = Path.Combine(uploadsDir, $"{metadata.UploadId}_{Path.GetFileName(metadata.FileName)}");
                try
                {
                    if (File.Exists(finalPath)) File.Delete(finalPath);
                    File.Move(tempFilePath, finalPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[MockServer] Could not move temp upload file to final location");
                }
            }

            return new VideoUploadResponse
            {
                Success = true,
                Message = "Upload successful"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MockServer] Exception");
            return new VideoUploadResponse
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }
}
