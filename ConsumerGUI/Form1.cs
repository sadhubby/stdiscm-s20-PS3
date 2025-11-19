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
    private PictureBox currentPreview;

    public Form1()
    {
        InitializeComponent();
        LoadVideos();
        RenderThumbnails();
        SetupPreviewTimer();
    }

    private void LoadVideos()
    {
        videos.Add(new VideoItem(
            "RivalsAhh.mp4",
            "C:\\Users\\Nitro 5\\Videos\\MarvelRivals\\Highlights\\RivalsAhh.mp4"));
    }

    private void RenderThumbnails()
    {
        flowPanelVideos.Controls.Clear();

        foreach (var video in videos)
        {
            PictureBox thumb = new PictureBox();
            thumb.Width = 200;
            thumb.Height = 120;
            thumb.BorderStyle = BorderStyle.FixedSingle;
            thumb.SizeMode = PictureBoxSizeMode.StretchImage;

            thumb.Image = Properties.Resources.default_thumb;
            thumb.Tag = video;

            thumb.MouseHover += ThumbnailHover;
            thumb.MouseLeave += ThumbnailLeave;
            thumb.Click += ThumbnailClick;

            flowPanelVideos.Controls.Add(thumb);
        }
    }

    private void SetupPreviewTimer()
    {
        previewTimer = new Timer();
        previewTimer.Interval = 400;
        previewTimer.Tick += PreviewTick;
    }

    private void ThumbnailHover(object sender, EventArgs e)
    {
        currentPreview = sender as PictureBox;
        currentPreview.BorderStyle = BorderStyle.Fixed3D;

        previewTimer.Start();
    }

    private void ThumbnailLeave(object sender, EventArgs e)
    {
        previewTimer.Stop();
        currentPreview.BackColor = Color.White;

        var pb = sender as PictureBox;
        pb.BorderStyle = BorderStyle.FixedSingle;
    }

    private int frame = 0;
    private void PreviewTick(object sender, EventArgs e)
    {
        if (currentPreview == null) return;

        frame++;
        currentPreview.BackColor =
            (frame % 2 == 0) ? Color.LightGray : Color.White;
    }

    private void ThumbnailClick(object sender, EventArgs e)
    {
        var pb = sender as PictureBox;
        var vid = (VideoItem)pb.Tag;

        var player = new VideoPlayerForm(vid.FilePath);
        player.Show();
    }
}
