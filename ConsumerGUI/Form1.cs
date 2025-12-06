//using System;
//using System.Collections.Generic;
//using System.Drawing;
//using System.IO;
//using System.Windows.Forms;
//using Grpc.Net.Client;
//using stdiscm_PS3;
//using static stdiscm_PS3.VideoLibraryService;
//namespace ConsumerGUI
//{
//    public partial class Form1 : Form
//    {
//        private List<VideoItem> videos = new List<VideoItem>();
//        private PictureBox? currentPreviewBox;
//        private AxWMPLib.AxWindowsMediaPlayer hoverPlayer = null!;
//        private System.Windows.Forms.Timer previewTimer = null!;
//        private GrpcChannel? _channel;
//        private stdiscm_PS3.VideoLibraryService.VideoLibraryServiceClient? _libraryClient;
//        private readonly string _serverAddress = "http://localhost:5001"; // Default gRPC server address
//        private System.Windows.Forms.Timer refreshTimer = null!;
//        public Form1()
//        {
//            InitializeComponent();
//            InitHoverPlayer();
//            InitPreviewTimer();
//            InitGrpcClient();
//            InitRefreshTimer();
//            LoadMockVideos();
//            _ = RefreshVideoList();
//        }

//        private void InitGrpcClient()
//        {
//            try
//            {
//                _channel = GrpcChannel.ForAddress(_serverAddress);
//                _libraryClient = new VideoLibraryServiceClient(_channel);
//            }
//            catch
//            {
//                _channel = null;
//                _libraryClient = null;
//            }
//        }
//        private void LoadMockVideos()
//        {
//            string uploadsPath = Path.GetFullPath(
//                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"C:\Users\Evan\Documents\GitHub\stdiscm-PS3\uploads")
//            );

//            videos.Add(new VideoItem("a4e004921424420480bba44cc3a07d70_clip7.mp4", Path.Combine(uploadsPath, "a4e004921424420480bba44cc3a07d70_clip7.mp4")));
//        }
//        private void RenderThumbnails()
//        {
//            flowPanelVideos.Controls.Clear();

//            foreach (var vid in videos)
//            {
//                // generate (or load cached) thumbnail - this may take a bit for new items
//                Image thumb = VideoThumbnailer.GetThumbnail(vid.FilePath, 320, 180);
//                var info = new ThumbnailInfo(vid.FilePath, thumb);
//                var pb = new PictureBox
//                {
//                    Width = 200,
//                    Height = 120,
//                    SizeMode = PictureBoxSizeMode.StretchImage,
//                    BorderStyle = BorderStyle.FixedSingle,
//                    Image = info.Thumbnail,
//                    Tag = info,
//                };

//                pb.MouseHover += ThumbnailMouseHover;
//                pb.MouseLeave += ThumbnailMouseLeave;
//                pb.Click += ThumbnailClick;

//                flowPanelVideos.Controls.Add(pb);
//            }
//        }

//        private void ThumbnailMouseHover(object? sender, EventArgs e)
//        {
//            var pb = (PictureBox)sender!;
//            var info = (ThumbnailInfo)pb.Tag!;
//            currentPreviewBox = pb;

//            ShowPreview(info, pb);
//            previewTimer.Start();
//        }

//        private void PreviewTimerTick(object? sender, EventArgs e)
//        {
//            if (currentPreviewBox == null) return;

//            // check if mouse is still within thumbnail bounds (convert to form client coords)
//            var rect = currentPreviewBox.Bounds;
//            // Bounds are relative to parent (flowPanel). Need to translate mouse to parent coordinates
//            var cursor = PointToClient(Cursor.Position);
//            var parentPoint = currentPreviewBox.Parent.PointToClient(Cursor.Position);
//            if (!currentPreviewBox.Bounds.Contains(parentPoint))
//            {
//                StopPreview();
//            }
//        }

//        private void ShowPreview(ThumbnailInfo info, PictureBox pb)
//        {
//            try
//            {
//                if (!Uri.IsWellFormedUriString(info.Path, UriKind.Absolute))
//                {
//                    // if not absolute, treat as local file path
//                    if (!File.Exists(info.Path)) return;
//                }

//                hoverPlayer.URL = info.Path;
//                hoverPlayer.settings.autoStart = true;
//                hoverPlayer.Ctlcontrols.currentPosition = 0;

//                hoverPlayer.Bounds = pb.Bounds;
//                hoverPlayer.Parent = pb.Parent;
//                hoverPlayer.BringToFront();
//                hoverPlayer.Visible = true;
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine("ShowPreview error: " + ex.Message);
//            }
//        }

//        private void StopPreview()
//        {
//            try
//            {
//                hoverPlayer.Ctlcontrols.stop();
//                hoverPlayer.Visible = false;
//            }
//            catch { }

//            if (currentPreviewBox != null)
//            {
//                var info = (ThumbnailInfo)currentPreviewBox.Tag;
//                currentPreviewBox.Image = info.Thumbnail;
//            }

//            previewTimer.Stop();
//            currentPreviewBox = null;
//        }

//        private void ThumbnailMouseLeave(object? sender, EventArgs e)
//        {
//            StopPreview();
//        }

//        private void ThumbnailClick(object? sender, EventArgs e)
//        {
//            // stop preview first so main player starts clean
//            StopPreview();

//            var pb = (PictureBox)sender!;
//            var info = (ThumbnailInfo)pb.Tag!;
//            try
//            {
//                VideoPlayer.URL = info.Path; // plays HTTP URL
//            }
//            catch (Exception ex)
//            {
//                MessageBox.Show("Cannot play the selected video: " + ex.Message);
//            }
//        }

//         private void InitHoverPlayer()
//        {
//            hoverPlayer = new AxWMPLib.AxWindowsMediaPlayer();
//            hoverPlayer.CreateControl();
//            hoverPlayer.uiMode = "none";
//            hoverPlayer.Visible = false;
//            try { hoverPlayer.settings.mute = true; } catch { } // ignore if not ready
//            this.Controls.Add(hoverPlayer);

//            // clicking the hover player will stop preview and play in main player
//            hoverPlayer.ClickEvent += (s, e) =>
//            {
//                if (currentPreviewBox != null)
//                {
//                    var info = (ThumbnailInfo)currentPreviewBox.Tag;
//                    StopPreview();
//                    VideoPlayer.URL = info.Path;
//                }
//            };
//        }
//        private void InitPreviewTimer()
//        {
//            previewTimer = new System.Windows.Forms.Timer { Interval = 150 };
//            previewTimer.Tick += PreviewTimerTick;
//        }

//        private void InitRefreshTimer()
//        {
//            refreshTimer = new System.Windows.Forms.Timer { Interval = 5000 }; // 60s
//            refreshTimer.Tick += async (s, e) => await RefreshVideoList();
//            refreshTimer.Start();
//        }

//        private async Task RefreshVideoList()
//        {
//            try
//            {
//                if (_libraryClient == null) InitGrpcClient();
//                if (_libraryClient == null) return;

//                var resp = await _libraryClient!.ListVideosAsync(new ListVideosRequest());
//                if (resp == null) return;

//                // simple update: if counts differ or names changed, rebuild list
//                var newUrls = resp.Videos.Select(v => (v.FileName, v.PlaybackUrl)).ToList();

//                bool changed = newUrls.Count != videos.Count;
//                if (!changed)
//                {
//                    // quick check: also check if any new name appears
//                    for (int i = 0; i < videos.Count; ++i)
//                    {
//                        if (videos[i].FilePath != newUrls[i].PlaybackUrl)
//                        {
//                            changed = true; break;
//                        }
//                    }
//                }

//                if (!changed)
//                {
//                    // maybe order changed: do a safer compare using set
//                    var setOld = videos.Select(v => v.FilePath).ToHashSet();
//                    var setNew = newUrls.Select(t => t.PlaybackUrl).ToHashSet();
//                    if (!setOld.SetEquals(setNew)) changed = true;
//                }

//                if (changed)
//                {
//                    // rebuild videos list
//                    videos.Clear();
//                    foreach (var vi in resp.Videos)
//                    {
//                        // playback url is expected to be an absolute http url
//                        var url = vi.PlaybackUrl;
//                        if (string.IsNullOrWhiteSpace(url))
//                        {
//                            // fallback: try file path (not expected)
//                            url = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\..\\..\\Consumer\\Uploads", vi.FileName);
//                        }
//                        videos.Add(new VideoItem(vi.FileName, url));
//                    }

//                    // UI update must be on UI thread
//                    if (InvokeRequired) Invoke(new Action(RenderThumbnails));
//                    else RenderThumbnails();
//                }
//            }
//            catch (Exception ex)
//            {
//                // server offline or network problem: log to debug window, keep showing previous items
//                Console.WriteLine("RefreshVideoList error: " + ex.Message);
//            }
//        }

//        private class ThumbnailInfo
//        {
//            public string Path { get; }
//            public Image Thumbnail { get; }

//            public ThumbnailInfo(string path, Image thumb)
//            {
//                Path = path;
//                Thumbnail = thumb;
//            }
//        }
//        protected override void OnFormClosing(FormClosingEventArgs e)
//        {
//            try
//            {
//                refreshTimer?.Stop();
//                previewTimer?.Stop();
//                _channel?.Dispose();
//            }
//            catch { }
//            base.OnFormClosing(e);
//        }
//    }
//}
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ConsumerGUI
{
    public partial class Form1 : Form
    {
        private List<VideoItem> videos = new List<VideoItem>();
        private PictureBox currentPreviewBox;
        private AxWMPLib.AxWindowsMediaPlayer hoverPlayer;
        private System.Windows.Forms.Timer previewTimer;

        public Form1()
        {
            InitializeComponent();
            InitHoverPlayer();
            InitPreviewTimer();
            LoadMockVideos();
            RenderThumbnails();
        }

        private void LoadMockVideos()
        {
            string uploadsPath = Path.GetFullPath(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"C:\Users\Evan\Documents\GitHub\stdiscm-PS3\uploads")
            );

            videos.Add(new VideoItem("a4e004921424420480bba44cc3a07d70_clip7.mp4", Path.Combine(uploadsPath, "a4e004921424420480bba44cc3a07d70_clip7.mp4")));
        }

        private void RenderThumbnails()
        {
            flowPanelVideos.Controls.Clear();

            foreach (var vid in videos)
            {
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
            // do nothing muna haha
        }

        private void ThumbnailClick(object sender, EventArgs e)
        {
            StopPreview(); //ADDED 12-3-2025

            var pb = (PictureBox)sender;
            var info = (ThumbnailInfo)pb.Tag;

            hoverPlayer.Visible = false;

            hoverPlayer.settings.mute = true;

            this.Controls.Add(hoverPlayer);
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
    }
}