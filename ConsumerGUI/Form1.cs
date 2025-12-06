using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ConsumerGUI
{
    public partial class Form1 : Form
    {
        private List<VideoItem> videos = new();
        private PictureBox currentPreviewBox;
        private AxWMPLib.AxWindowsMediaPlayer hoverPlayer;
        private System.Windows.Forms.Timer previewTimer;

        // Path to uploads folder
        private readonly string uploadsPath =
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\..\uploads"));

        public Form1()
        {
            InitializeComponent();
            InitHoverPlayer();
            InitPreviewTimer();

            LoadVideosFromUploads();
            RenderThumbnails();
        }

        private void LoadVideosFromUploads()
        {
            videos.Clear();

            if (!Directory.Exists(uploadsPath))
            {
                MessageBox.Show("Uploads folder missing:\n" + uploadsPath);
                return;
            }

            string[] videoExt = { ".mp4", ".mov", ".mkv", ".avi", ".wmv", ".webm", ".mpeg", ".mpg", ".m4v" };

            foreach (string file in Directory.GetFiles(uploadsPath))
            {
                if (Array.Exists(videoExt, e => e.Equals(Path.GetExtension(file), StringComparison.OrdinalIgnoreCase)))
                {
                    videos.Add(new VideoItem(Path.GetFileName(file), file));
                }
            }
        }

        private void RenderThumbnails()
        {
            flowPanelVideos.Controls.Clear();

            foreach (var vid in videos)
            {
                Image thumb = VideoThumbnailer.GetThumbnail(vid.FilePath);

                PictureBox pb = new()
                {
                    Width = 200,
                    Height = 120,
                    SizeMode = PictureBoxSizeMode.StretchImage,
                    BorderStyle = BorderStyle.FixedSingle,
                    Image = thumb,
                    Tag = new ThumbnailInfo(vid.FilePath, thumb)
                };

                pb.MouseHover += ThumbnailMouseHover;
                pb.MouseLeave += ThumbnailMouseLeave;
                pb.Click += ThumbnailClick;

                flowPanelVideos.Controls.Add(pb);
            }
        }

        private void ThumbnailMouseHover(object sender, EventArgs e)
        {
            currentPreviewBox = (PictureBox)sender;
            var info = (ThumbnailInfo)currentPreviewBox.Tag;

            ShowPreview(info, currentPreviewBox);
            previewTimer.Start();
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

        private void PreviewTimerTick(object sender, EventArgs e)
        {
            if (currentPreviewBox == null) return;

            // If mouse leaves thumbnail area → stop preview
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
            // Timer checks mouse position — no work here
        }

        private void ThumbnailClick(object sender, EventArgs e)
        {
            StopPreview();  // ensure preview closes cleanly

            var pb = (PictureBox)sender;
            var info = (ThumbnailInfo)pb.Tag;

            // 🔥 Play selected video on the right-side VideoPlayer
            VideoPlayer.URL = info.Path;
            VideoPlayer.Ctlcontrols.play();
        }

        private void InitHoverPlayer()
        {
            hoverPlayer = new AxWMPLib.AxWindowsMediaPlayer();
            hoverPlayer.CreateControl();
            hoverPlayer.uiMode = "none";
            hoverPlayer.settings.mute = true;
            hoverPlayer.Visible = false;

            this.Controls.Add(hoverPlayer);
        }

        private void InitPreviewTimer()
        {
            previewTimer = new System.Windows.Forms.Timer() { Interval = 100 };
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
