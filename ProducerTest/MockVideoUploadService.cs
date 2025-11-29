using Grpc.Core;
using stdiscm_PS3;

namespace ProducerTest;

/// <summary>
/// Mock VideoUploadService for testing Producer uploads.
/// Accepts uploads, logs them, and can simulate queue-full responses.
/// </summary>
public class MockVideoUploadService : VideoUploadService.VideoUploadServiceBase
{
    private readonly ILogger<MockVideoUploadService> _logger;
    private static int _uploadCount = 0;
    private static readonly object _lock = new();

    public MockVideoUploadService(ILogger<MockVideoUploadService> logger)
    {
        _logger = logger;
    }

    public override async Task<VideoUploadResponse> UploadVideo(
        IAsyncStreamReader<VideoUploadRequest> requestStream,
        ServerCallContext context)
    {
        try
        {
            VideoMetadata? metadata = null;
            long totalBytes = 0;
            int chunkCount = 0;

            await foreach (var req in requestStream.ReadAllAsync())
            {
                if (req.RequestCase == VideoUploadRequest.RequestOneofCase.Metadata)
                {
                    metadata = req.Metadata;
                    _logger.LogInformation($"[MockServer] Upload started: {metadata.FileName} (ID: {metadata.UploadId})");
                }
                else if (req.RequestCase == VideoUploadRequest.RequestOneofCase.Chunk)
                {
                    totalBytes += req.Chunk.Length;
                    chunkCount++;
                    _logger.LogDebug($"[MockServer] Chunk {chunkCount}: {req.Chunk.Length} bytes");
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

            lock (_lock)
            {
                _uploadCount++;
            }

            _logger.LogInformation($"[MockServer] Upload complete: {metadata.FileName}, {totalBytes} bytes in {chunkCount} chunks");

            // Simulate queue-full every 3rd upload for testing retries
            bool isFull = false;
            lock (_lock)
            {
                isFull = (_uploadCount % 3 == 0);
            }

            if (isFull)
            {
                _logger.LogWarning("[MockServer] Simulating queue full!");
                return new VideoUploadResponse
                {
                    Success = false,
                    Message = "Queue is full, please retry later"
                };
            }
            else
            {
                return new VideoUploadResponse
                {
                    Success = true,
                    Message = "Upload successful"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"[MockServer] Exception: {ex.Message}");
            return new VideoUploadResponse
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }
}
