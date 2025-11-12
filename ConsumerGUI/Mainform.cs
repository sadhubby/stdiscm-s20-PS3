using Grpc.Net.Client;
using stdiscm_PS3;
using System.Windows.Forms;

namespace ConsumerGUI
{
    public partial class MainForm : Form
    {
        private readonly VideoLibraryService.VideoLibraryServiceClient _client;

        public MainForm()
        {
            InitializeComponent();
            var channel = GrpcChannel.ForAddress("https://localhost:5001");
            _client = new VideoLibraryService.VideoLibraryServiceClient(channel);
            LoadVideos();
        }

        private async void LoadVideos()
        {
            var response = await _client.ListVideosAsync(new ListVideosRequest());
            foreach (var video in response.Videos)
            {
                var btn = new Button
                {
                    Text = $"{video.FileName} ({video.SizeInBytes / 1024} KB)",
                    Tag = video.PlaybackUrl,
                    Width = 300,
                    Height = 40
                };
                btn.MouseHover += (s, e) => ShowPreview(video.PlaybackUrl);
                btn.Click += (s, e) => PlayVideo(video.PlaybackUrl);
                flowLayoutPanel1.Controls.Add(btn);
                //FlowLayoutPanel lists uploaded videos
            }
        }

        private void ShowPreview(string url)
        {
            // Play first 10 secs usin embedded player
            videoPlayer.URL = url;
            videoPlayer.Ctlcontrols.play();
            Task.Delay(10000).ContinueWith(_ => videoPlayer.Ctlcontrols.stop());
        }

        private void PlayVideo(string url)
        {
            videoPlayer.URL = url;
            videoPlayer.Ctlcontrols.play();
        }
    }
}
