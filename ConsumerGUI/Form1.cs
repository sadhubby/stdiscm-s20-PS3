using Grpc.Net.Client;
using StreamingProtos;   // generated proto classes (VideoLibraryService, VideoInfo, ...)
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ConsumerGUI
{
    public partial class Form1 : Form
    {
        private readonly List<VideoItem> videos = new List<VideoItem>();
        private PictureBox currentPreviewBox;
        private AxWMPLib.AxWindowsMediaPlayer hoverPlayer;
        private Timer previewTimer;
        private Timer refreshTimer;

        // gRPC
        private GrpcChannel _channel;
        private VideoLibraryService.VideoLibraryServiceClient _libraryClient;
        private readonly string _serverAddress = "http://localhost:5000";

        public Form1()
        {
            InitializeComponent();

            InitHoverPlayer();
            InitPreviewTimer();
            InitGrpcClient();
            InitRefreshTimer();

            // initial load (async)
            _ = RefreshVideoList();
        }

        private void InitGrpcClient()
        {
            try
            {
                _channel = GrpcChannel.ForAddress(_serverAddress);
                _libraryClient = new VideoLibraryService.VideoLibraryServiceClient(_channel);
            }
            catch
            {
                _channel = null;
                _libraryClient = null;
            }
        }

        private void InitHoverPlayer()
        {
            hoverPlayer = new AxWMPLib.AxWindowsMediaPlayer();
            hoverPlayer.CreateControl();
            hoverPlayer.uiMode = "none";
            hoverPlayer.Visible = false;
            try { hoverPlayer.settings.mute = true; } catch { } // ignore if not ready
            this.Controls.Add(hoverPlayer);

            // clicking the hover player will stop preview and play in main player
            hoverPlayer.ClickEvent += (s, e) =>
            {
                if (currentPreviewBox != null)
                {
                    var info = (ThumbnailInfo)currentPreviewBox.Tag;
                    StopPreview();
                    VideoPlayer.URL = info.Path;
                }
            };
        }

        private void InitPreviewTimer()
        {
            previewTimer = new Timer { Interval = 150 };
            previewTimer.Tick += PreviewTimer_Tick;
        }

        private void InitRefreshTimer()
        {
            refreshTimer = new Timer { Interval = 5000 };
            refreshTimer.Tick += async (s, e) => await RefreshVideoList();
            refreshTimer.Start();
        }

        private async Task RefreshVideoList()
        {
            try
            {
                if (_libraryClient == null) InitGrpcClient();
                if (_libraryClient == null) return;

                var resp = await _libraryClient.ListVideosAsync(new ListVideosRequest());
                if (resp == null) return;

                // simple update: if counts differ or names changed, rebuild list
                var newUrls = resp.Videos.Select(v => (v.FileName, v.PlaybackUrl)).ToList();

                bool changed = newUrls.Count != videos.Count ||
                    newUrls.Where((t, i) => i < videos.Count && videos[i].PlaybackUrl != t.PlaybackUrl).Any();

                if (!changed)
                {
                    // quick check: also check if any new name appears
                    for (int i = 0; i < newUrls.Count && i < videos.Count; ++i)
                    {
                        if (!string.Equals(newUrls[i].PlaybackUrl, videos[i].PlaybackUrl, StringComparison.Ordinal))
                        {
                            changed = true; break;
                        }
                    }
                }

                if (!changed)
                {
                    // maybe order changed: do a safer compare using set
                    var setOld = videos.Select(v => v.PlaybackUrl).ToHashSet();
                    var setNew = newUrls.Select(t => t.PlaybackUrl).ToHashSet();
                    if (!setOld.SetEquals(setNew)) changed = true;
                }

                if (changed)
                {
                    // rebuild videos list
                    videos.Clear();
                    foreach (var vi in resp.Videos)
                    {
                        // playback url is expected to be an absolute http url
                        var url = vi.PlaybackUrl;
                        if (string.IsNullOrWhiteSpace(url))
                        {
                            // fallback: try file path (not expected)
                            url = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\..\\..\\Consumer\\Uploads", vi.FileName);
                        }
                        videos.Add(new VideoItem(vi.FileName, url));
                    }

                    // UI update must be on UI thread
                    if (InvokeRequired) Invoke(new Action(RenderThumbnails));
                    else RenderThumbnails();
                }
            }
            catch (Exception ex)
            {
                // server offline or network problem: log to debug window, keep showing previous items
                Console.WriteLine("RefreshVideoList error: " + ex.Message);
            }
        }

        private void RenderThumbnails()
        {
            flowPanelVideos.Controls.Clear();

            foreach (var vid in videos)
            {
                // generate (or load cached) thumbnail - this may take a bit for new items
                Image thumb = VideoThumbnailer.GetThumbnail(vid.PlaybackUrl, 320, 180);

                var pb = new PictureBox
                {
                    Width = 200,
                    Height = 120,
                    SizeMode = PictureBoxSizeMode.StretchImage,
                    BorderStyle = BorderStyle.FixedSingle,
                    Image = thumb
                };

                pb.Tag = new ThumbnailInfo(vid.PlaybackUrl, thumb);

                pb.MouseHover += ThumbnailMouseHover;
                pb.MouseLeave += ThumbnailMouseLeave;
                pb.Click += ThumbnailClick;

                flowPanelVideos.Controls.Add(pb);
            }
        }

        private void ThumbnailMouseHover(object sender, EventArgs e)
        {
            var pb = (PictureBox)sender;
            var info = (ThumbnailInfo)pb.Tag;
            currentPreviewBox = pb;

            ShowPreview(info, pb);
            previewTimer.Start();
        }

        private void ThumbnailMouseLeave(object sender, EventArgs e)
        {
            // let PreviewTimerTick decide when mouse actually left (prevents flicker when overlay receives events)
        }

        private void PreviewTimer_Tick(object? sender, EventArgs e)
        {
            if (currentPreviewBox == null) return;

            // check if mouse is still within thumbnail bounds (convert to form client coords)
            var rect = currentPreviewBox.Bounds;
            // Bounds are relative to parent (flowPanel). Need to translate mouse to parent coordinates
            var cursor = PointToClient(Cursor.Position);
            var parentPoint = currentPreviewBox.Parent.PointToClient(Cursor.Position);
            if (!currentPreviewBox.Bounds.Contains(parentPoint))
            {
                StopPreview();
            }
        }

        private void ShowPreview(ThumbnailInfo info, PictureBox pb)
        {
            try
            {
                if (!Uri.IsWellFormedUriString(info.Path, UriKind.Absolute))
                {
                    // if not absolute, treat as local file path
                    if (!File.Exists(info.Path)) return;
                }

                hoverPlayer.URL = info.Path;
                hoverPlayer.settings.autoStart = true;
                hoverPlayer.Ctlcontrols.currentPosition = 0;

                hoverPlayer.Bounds = pb.Bounds;
                hoverPlayer.Parent = pb.Parent;
                hoverPlayer.BringToFront();
                hoverPlayer.Visible = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("ShowPreview error: " + ex.Message);
            }
        }

        private void StopPreview()
        {
            try
            {
                hoverPlayer.Ctlcontrols.stop();
                hoverPlayer.Visible = false;
            }
            catch { }

            if (currentPreviewBox != null)
            {
                var info = (ThumbnailInfo)currentPreviewBox.Tag;
                currentPreviewBox.Image = info.Thumbnail;
            }

            previewTimer.Stop();
            currentPreviewBox = null;
        }

        private void ThumbnailClick(object sender, EventArgs e)
        {
            // stop preview first so main player starts clean
            StopPreview();

            var pb = (PictureBox)sender;
            var info = (ThumbnailInfo)pb.Tag;
            try
            {
                VideoPlayer.URL = info.Path; // plays HTTP URL
            }
            catch (Exception ex)
            {
                MessageBox.Show("Cannot play the selected video: " + ex.Message);
            }
        }

        // small helper class used in tags
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

        // cleanup on close
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
