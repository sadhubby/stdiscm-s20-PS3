using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

public partial class Form1 : Form
{
    private List<VideoItem> videos = new List<VideoItem>();
    private Timer previewTimer;
    private PictureBox currentPreviewBox;

    public Form1()
    {
        InitializeComponent();
        LoadMockVideos();
        RenderThumbnails();

        previewTimer = new Timer();
        previewTimer.Interval = 300;
        previewTimer.Tick += PreviewTimerTick;
    }

    private void LoadMockVideos()
    {
        videos.Add(new VideoItem(
            "RivalsAhh.mp4",
            @"C:\Users\Nitro 5\Videos\MarvelRivals\Highlights\RivalsAhh.mp4"
        ));
    }

    private void RenderThumbnails()
    {
        flowPanelVideos.Controls.Clear();

        foreach (var vid in videos)
        {
            var pb = new PictureBox();
            pb.Width = 160;
            pb.Height = 100;
            pb.SizeMode = PictureBoxSizeMode.StretchImage;
            pb.BorderStyle = BorderStyle.FixedSingle;

            // Placeholder image
            pb.Image = Properties.Resources.default_thumb;

            pb.Tag = vid;

            pb.MouseHover += ThumbnailMouseHover;
            pb.MouseLeave += ThumbnailMouseLeave;
            pb.Click += ThumbnailClick;

            flowPanelVideos.Controls.Add(pb);
        }
    }

    // ------- Preview animation on hover --------

    private int previewFrame = 0;

    private void ThumbnailMouseHover(object sender, EventArgs e)
    {
        currentPreviewBox = sender as PictureBox;
        currentPreviewBox.BorderStyle = BorderStyle.Fixed3D;

        previewTimer.Start();
    }

    private void ThumbnailMouseLeave(object sender, EventArgs e)
    {
        previewTimer.Stop();

        var pb = sender as PictureBox;
        pb.BorderStyle = BorderStyle.FixedSingle;

        pb.Image = Properties.Resources.default_thumb;
    }

    private void PreviewTimerTick(object sender, EventArgs e)
    {
        if (currentPreviewBox == null) return;

        previewFrame++;

        // simple blinking effect
        currentPreviewBox.BackColor =
            (previewFrame % 2 == 0 ? Color.LightGray : Color.White);
    }

    // ------- Click to play video on the right panel --------

    private void ThumbnailClick(object sender, EventArgs e)
    {
        var pb = sender as PictureBox;
        var video = (VideoItem)pb.Tag;

        videoPlayer.URL = video.FilePath;
        videoPlayer.Ctlcontrols.play();
    }
}

