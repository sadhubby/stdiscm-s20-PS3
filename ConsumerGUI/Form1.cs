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
        private PictureBox? currentPreviewBox;
        private AxWMPLib.AxWindowsMediaPlayer hoverPlayer = null!;
        private System.Windows.Forms.Timer previewTimer = null!;

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
            /*@TODO 
            * Load videos in the video player. 
            * 
            * Currently hardcoded to have the videos prepared beforehand in the folder stdiscm-ps3 -> Consumer -> Uploads
            * Make this not be hardcoded but instead get from producer that will save it in the consumer uploads folder. 
            * 
            */
            string uploadsPath = Path.GetFullPath(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\Consumer\Uploads")
            );

            videos.Add(new VideoItem("RivalsAhh.mp4", Path.Combine(uploadsPath, "RivalsAhh.mp4")));
            videos.Add(new VideoItem("Taboo Gameplay 1.mp4", Path.Combine(uploadsPath, "Taboo Gameplay 1.mp4")));
            videos.Add(new VideoItem("Taboo Gameplay 3.mp4", Path.Combine(uploadsPath, "Taboo Gameplay 3.mp4")));
            videos.Add(new VideoItem("Taboo Gameplay 4.mp4", Path.Combine(uploadsPath, "Taboo Gameplay 4.mp4")));
            videos.Add(new VideoItem("Taboo Gameplay Furnace.mp4", Path.Combine(uploadsPath, "Taboo Gameplay Furnace.mp4")));
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

        private void ThumbnailMouseHover(object? sender, EventArgs e)
        {
            var pb = (PictureBox)sender!;
            var info = (ThumbnailInfo)pb.Tag!;
            currentPreviewBox = pb;

            ShowPreview(info, pb);
            previewTimer.Start();
        }

        private void PreviewTimerTick(object? sender, EventArgs e)
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

        private void ThumbnailMouseLeave(object? sender, EventArgs e)
        {
            // do nothing muna haha
        }

        private void ThumbnailClick(object? sender, EventArgs e)
        {
            var pb = (PictureBox)sender!;
            var info = (ThumbnailInfo)pb.Tag!;

            VideoPlayer.URL = info.Path;
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
