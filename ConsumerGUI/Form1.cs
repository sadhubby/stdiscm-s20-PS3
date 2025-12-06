using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

using stdiscm_PS3;          // gRPC generated classes from streaming.proto
using Grpc.Net.Client;      // modern gRPC channel API

namespace ConsumerGUI
{
    public partial class Form1 : Form
    {
        private List<VideoItem> videos = new List<VideoItem>();
        private PictureBox currentPreviewBox;
        private AxWMPLib.AxWindowsMediaPlayer hoverPlayer;
        private System.Windows.Forms.Timer previewTimer;

        // gRPC
        private GrpcChannel _channel;
        private VideoLibraryService.VideoLibraryServiceClient _client;

        public Form1()
        {
            InitializeComponent();
            InitGrpc();
            InitHoverPlayer();
            InitPreviewTimer();

            _ = RefreshVideoList(); // async load
        }

        private void InitGrpc()
        {
            _channel = GrpcChannel.ForAddress("http://localhost:5000");
            _client = new VideoLibraryService.VideoLibraryServiceClient(_channel);
        }

        private async System.Threading.Tasks.Task RefreshVideoList()
        {
            try
            {
                var response = await _client.ListVideosAsync(new ListVideosRequest());
                videos.Clear();

                foreach (var v in response.Videos)
                {
                    videos.Add(new VideoItem(v.FileName, v.PlaybackUrl));
                }

                await RenderThumbnails();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to fetch video list: " + ex.Message);
            }
        }

        private async System.Threading.Tasks.Task RenderThumbnails()
        {
            flowPanelVideos.Controls.Clear();

            foreach (var vid in videos)
            {
                // Download HTTP video → temp → ffmpeg thumbnail
                string localTemp = await HttpVideoFetcher.DownloadToTemp(vid.FilePath);
                var thumbnail = VideoThumbnailer.GetThumbnail(localTemp);
                try { File.Delete(localTemp); } catch { }

                PictureBox pb = new PictureBox
                {
                    Width = 200,
                    Height = 120,
                    SizeMode = PictureBoxSizeMode.StretchImage,
                    BorderStyle = BorderStyle.FixedSingle,
                    Image = thumbnail,
                    Tag = new ThumbnailInfo(vid.FilePath, thumbnail)
                };

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

        private void ShowPreview(ThumbnailInfo info, PictureBox pb)
        {
            hoverPlayer.URL = info.Path; // HTTP playback URL
            hoverPlayer.settings.autoStart = true;
            hoverPlayer.Ctlcontrols.currentPosition = 0;

            hoverPlayer.Bounds = pb.Bounds;
            hoverPlayer.Parent = pb.Parent;
            hoverPlayer.BringToFront();
            hoverPlayer.Visible = true;
        }

        private void PreviewTimerTick(object sender, EventArgs e)
        {
            if (currentPreviewBox == null) return;

            if (!currentPreviewBox.Bounds.Contains(PointToClient(Cursor.Position)))
                StopPreview();
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
            // handled via timer
        }

        private void ThumbnailClick(object sender, EventArgs e)
        {
            StopPreview();

            var pb = (PictureBox)sender;
            var info = (ThumbnailInfo)pb.Tag;

            VideoPlayer.URL = info.Path; // open full streaming playback
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
            previewTimer = new System.Windows.Forms.Timer { Interval = 100 };
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
