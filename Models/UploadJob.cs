using System;

namespace ConsumerBackend.Models
{
    public class UploadJob
    {
        public string JobId { get; set; } = Guid.NewGuid().ToString("N");
        public string TempFilePath { get; set; } = null!;
        public string OriginalFileName { get; set; } = null!;
        public string? ChecksumSha256 { get; set; }
        public string ProducerId { get; set; } = string.Empty;
        public long SizeBytes { get; set; } = 0;
        public DateTimeOffset ReceivedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    }
}
