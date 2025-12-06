using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using Grpc.Net.Client;
using stdiscm_PS3;
using static stdiscm_PS3.VideoLibraryService;

namespace ConsumerGUI
{
    public partial class Form1 : Form
    {
        private List<VideoItem> videos = new List<VideoItem>();
        private PictureBox currentPreviewBox;
        private AxWMPLib.AxWindowsMediaPlayer hoverPlayer;
        private System.Windows.Forms.Timer previewTimer;
        private System.Windows.Forms.Timer refreshTimer;
        private GrpcChannel _channel;
        private VideoLibraryServiceClient _libraryClient;
        private readonly string _serverAddress = "http://localhost:5001"; // gRPC server address
        private readonly string _logFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "gui_debug.log");

        private void Log(string message)
        {
            try
            {
                string logEntry = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
                File.AppendAllText(_logFile, logEntry + Environment.NewLine);
            }
            catch { }
        }

        public Form1()
        {
            InitializeComponent();
            InitHoverPlayer();
            InitPreviewTimer();
            InitGrpcClient();
            InitRefreshTimer();
            _ = LoadMockVideos();
        }

        private void InitGrpcClient()
        {
            try
            {
                _channel = GrpcChannel.ForAddress(_serverAddress);
                _libraryClient = new VideoLibraryServiceClient(_channel);
                Log($"✓ gRPC client initialized successfully to {_serverAddress}");
            }
            catch (Exception ex)
            {
                Log($"✗ Failed to initialize gRPC client: {ex.Message}");
                _channel = null;
                _libraryClient = null;
            }
        }

        private async Task LoadMockVideos()
        {
            try
            {
                if (_libraryClient == null)
                {
                    Log("Initializing gRPC client...");
                    InitGrpcClient();
                }

                if (_libraryClient != null)
                {
                    Log("Calling ListVideos from Consumer...");
                    var resp = await _libraryClient.ListVideosAsync(new ListVideosRequest());
                    Log($"✓ Received {resp.Videos.Count} videos from Consumer");
                    videos.Clear();
                    foreach (var vi in resp.Videos)
                    {
                        Log($"  - {vi.FileName}");
                        videos.Add(new VideoItem(vi.FileName, vi.PlaybackUrl));
                    }
                }
                else
                {
                    Log("✗ gRPC client is null, cannot load videos");
                }
            }
            catch (Exception ex)
            {
                Log($"✗ LoadMockVideos error: {ex.Message}\n{ex.StackTrace}");
            }

            RenderThumbnails();
        }

        private void InitRefreshTimer()
        {
            refreshTimer = new System.Windows.Forms.Timer { Interval = 2000 }; // 2 seconds
            refreshTimer.Tick += async (s, e) => {
                Log($"[Timer] Refresh tick at {DateTime.Now:HH:mm:ss.fff}");
                await RefreshVideoList();
            };
            refreshTimer.Start();
            Log("✓ Refresh timer started (2 second interval)");
        }

        private async Task RefreshVideoList()
        {
            try
            {
                if (_libraryClient == null)
                {
                    Log("[RefreshVideoList] Client is null, reinitializing...");
                    InitGrpcClient();
                }

                if (_libraryClient == null)
                {
                    Log("[RefreshVideoList] ✗ Client still null after reinit");
                    return;
                }

                var resp = await _libraryClient.ListVideosAsync(new ListVideosRequest());
                if (resp == null)
                {
                    Log("[RefreshVideoList] ✗ Response is null");
                    return;
                }

                Log($"[RefreshVideoList] Server returned {resp.Videos.Count} videos");

                // Check if videos list changed
                var newVideos = resp.Videos.Select(v => (v.FileName, v.PlaybackUrl)).ToList();
                var oldVideos = videos.Select(v => (v.FileName, v.FilePath)).ToList();

                bool changed = newVideos.Count != oldVideos.Count;
                if (!changed)
                {
                    // Check if any video was added or removed
                    var newSet = newVideos.Select(v => v.FileName).ToHashSet();
                    var oldSet = oldVideos.Select(v => v.FileName).ToHashSet();
                    changed = !newSet.SetEquals(oldSet);
                }

                if (changed)
                {
                    Log($"[RefreshVideoList] ✓ Videos changed! Updating from {oldVideos.Count} to {newVideos.Count}");
                    videos.Clear();
                    foreach (var vi in resp.Videos)
                    {
                        Log($"  + {vi.FileName}");
                        Log($"    PlaybackUrl: {vi.PlaybackUrl}");
                        videos.Add(new VideoItem(vi.FileName, vi.PlaybackUrl));
                    }

                    // UI update must be on UI thread
                    if (InvokeRequired)
                    {
                        Invoke(new Action(RenderThumbnails));
                    }
                    else
                    {
                        RenderThumbnails();
                    }
                }
                else
                {
                    Log("[RefreshVideoList] No changes detected");
                }
            }
            catch (Exception ex)
            {
                Log($"[RefreshVideoList] ✗ Error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void RenderThumbnails()
        {
            Log("[RenderThumbnails] Starting render...");
            flowPanelVideos.Controls.Clear();

            foreach (var vid in videos)
            {
                Log($"[RenderThumbnails] Processing: {vid.FileName}, FilePath={vid.FilePath}");
                var thumbnail = VideoThumbnailer.GetThumbnail(vid.FilePath);

                PictureBox pb = new PictureBox
                {
                    Width = 200,
                    Height = 120,
                    SizeMode = PictureBoxSizeMode.StretchImage,
                    BorderStyle = BorderStyle.FixedSingle,
                    Image = thumbnail
                };

                pb.Tag = new ThumbnailInfo(vid.FilePath, thumbnail);

                pb.MouseHover += ThumbnailMouseHover;
                pb.MouseLeave += ThumbnailMouseLeave;
                pb.Click += ThumbnailClick;

                flowPanelVideos.Controls.Add(pb);
            }
            Log($"[RenderThumbnails] ✓ Rendered {videos.Count} videos");
        }

        private void ThumbnailMouseHover(object sender, EventArgs e)
        {
            var pb = (PictureBox)sender;
            var info = (ThumbnailInfo)pb.Tag;
            currentPreviewBox = pb;

            ShowPreview(info, pb);
            previewTimer.Start();
        }

        private void PreviewTimerTick(object sender, EventArgs e)
        {
            if (currentPreviewBox == null)
                return;

            // prevent flicker by checking real mouse position
            if (!currentPreviewBox.Bounds.Contains(PointToClient(Cursor.Position)))
            {
                StopPreview();
            }
        }

        private void ShowPreview(ThumbnailInfo info, PictureBox pb)
        {
            if (!File.Exists(info.Path))
                return;

            hoverPlayer.URL = info.Path;
            hoverPlayer.settings.autoStart = true;
            hoverPlayer.Ctlcontrols.currentPosition = 0;

            hoverPlayer.Bounds = pb.Bounds;
            hoverPlayer.Parent = pb.Parent;
            hoverPlayer.BringToFront();
            hoverPlayer.Visible = true;
        }

        private void StopPreview()
        {
            hoverPlayer.Ctlcontrols.stop();
            hoverPlayer.Visible = false;

            if (currentPreviewBox != null)
            {
                var info = (ThumbnailInfo)currentPreviewBox.Tag;
                currentPreviewBox.Image = info.Thumbnail;
            }

            previewTimer.Stop();
            currentPreviewBox = null;
        }

        private void ThumbnailMouseLeave(object sender, EventArgs e)
        {
            StopPreview();
        }

        private void ThumbnailClick(object sender, EventArgs e)
        {
            StopPreview();

            var pb = (PictureBox)sender;
            var info = (ThumbnailInfo)pb.Tag;

            hoverPlayer.Visible = false;
            hoverPlayer.settings.mute = true;

            // Download and play in background
            _ = DownloadAndPlayVideo(info.Path);
        }

        private async Task DownloadAndPlayVideo(string playbackUrl)
        {
            try
            {
                Log($"[DownloadAndPlayVideo] Starting download from: {playbackUrl}");
                
                // Create temp file for the video
                string tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".mp4");
                Log($"[DownloadAndPlayVideo] Temp file: {tempFile}");
                
                // Create HttpClient with HTTP/2 support
                var handler = new HttpClientHandler();
                using (var httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(10) })
                {
                    // Use HTTP/2 (default for .NET Core 3.0+)
                    httpClient.DefaultRequestVersion = new Version(2, 0);
                    
                    using (var response = await httpClient.GetAsync(playbackUrl, HttpCompletionOption.ResponseHeadersRead))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            Log($"[DownloadAndPlayVideo] ✗ HTTP Error: {response.StatusCode} {response.ReasonPhrase}");
                            var errorContent = await response.Content.ReadAsStringAsync();
                            Log($"[DownloadAndPlayVideo] Content: {errorContent}");
                            MessageBox.Show($"Failed to download video: {response.StatusCode} {response.ReasonPhrase}\n\nURL: {playbackUrl}\n\nError: {errorContent}");
                            return;
                        }
                        
                        Log($"[DownloadAndPlayVideo] ✓ Response received, starting download...");
                        
                        using (var contentStream = await response.Content.ReadAsStreamAsync())
                        {
                            using (var fileStream = File.Create(tempFile))
                            {
                                await contentStream.CopyToAsync(fileStream);
                            }
                        }
                    }
                }
                
                Log($"[DownloadAndPlayVideo] ✓ Download complete ({new FileInfo(tempFile).Length} bytes)");
                
                // Play the downloaded file on UI thread
                if (InvokeRequired)
                {
                    Invoke(new Action(() => PlayLocalVideo(tempFile)));
                }
                else
                {
                    PlayLocalVideo(tempFile);
                }
            }
            catch (Exception ex)
            {
                Log($"[DownloadAndPlayVideo] ✗ Error: {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show($"Cannot play video: {ex.Message}");
            }
        }

        private void PlayLocalVideo(string localFilePath)
        {
            try
            {
                Log($"[PlayLocalVideo] Playing from local file: {localFilePath}");
                
                // Clear playlist
                VideoPlayer.currentPlaylist.clear();
                
                // Create media item from local file
                WMPLib.IWMPMedia media = VideoPlayer.newMedia(localFilePath);
                
                // Add and play
                VideoPlayer.currentPlaylist.appendItem(media);
                VideoPlayer.Ctlcontrols.play();
                
                Log($"[PlayLocalVideo] ✓ Started playing");
            }
            catch (Exception ex)
            {
                Log($"[PlayLocalVideo] ✗ Error: {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show($"Cannot play video: {ex.Message}");
            }
        }

        private void InitHoverPlayer()
        {
            hoverPlayer = new AxWMPLib.AxWindowsMediaPlayer();
            hoverPlayer.CreateControl();
            hoverPlayer.uiMode = "none";
            hoverPlayer.Visible = false;
            hoverPlayer.settings.mute = true;
            this.Controls.Add(hoverPlayer);
        }

        private void InitPreviewTimer()
        {
            previewTimer = new System.Windows.Forms.Timer
            {
                Interval = 100
            };
            previewTimer.Tick += PreviewTimerTick;
        }

        private class ThumbnailInfo
        {
            public string Path { get; }
            public Image Thumbnail { get; }

            public ThumbnailInfo(string path, Image thumb)
            {
                Path = path;
                Thumbnail = thumb;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try
            {
                refreshTimer?.Stop();
                previewTimer?.Stop();
                _channel?.Dispose();
            }
            catch { }
            base.OnFormClosing(e);
        }
    }
}