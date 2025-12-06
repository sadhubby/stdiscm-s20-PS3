using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using stdiscm_PS3;

namespace stdiscm_PS3.Services
{
    public class VideoLibraryServiceImpl : VideoLibraryService.VideoLibraryServiceBase
    {
        private readonly ILogger<VideoLibraryServiceImpl> _logger;
        private readonly string _uploadFolder;
        private readonly string _httpBaseUrl;

        public VideoLibraryServiceImpl(ILogger<VideoLibraryServiceImpl> logger, string uploadFolder, string httpBaseUrl)
        {
            _logger = logger;
            _uploadFolder = uploadFolder;
            _httpBaseUrl = httpBaseUrl.TrimEnd('/');
        }

        public override Task<ListVideosResponse> ListVideos(ListVideosRequest request, ServerCallContext context)
        {
            var resp = new ListVideosResponse();
            try
            {
                if (!Directory.Exists(_uploadFolder))
                {
                    return Task.FromResult(resp);
                }

                var files = Directory.EnumerateFiles(_uploadFolder)
                    .Where(f => !f.EndsWith(".part")) // ignore temp
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(fi => fi.CreationTimeUtc)
                    .ToArray();

                foreach (var fi in files)
                {
                    var vid = new VideoInfo
                    {
                        VideoId = Path.GetFileNameWithoutExtension(fi.Name),
                        FileName = fi.Name,
                        SizeInBytes = fi.Length,
                        PlaybackUrl = $"{_httpBaseUrl}/media/{Uri.EscapeDataString(fi.Name)}",
                        UploadedUtcUnix = new DateTimeOffset(fi.CreationTimeUtc).ToUnixTimeSeconds()
                    };
                    resp.Videos.Add(vid);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ListVideos failed");
            }
            return Task.FromResult(resp);
        }
    }
}