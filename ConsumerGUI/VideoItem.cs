using System;
// THIS WAS THE MOCK VERSION
//namespace ConsumerGUI
//{
//    public class VideoItem
//    {
//        public string FileName { get; set; }
//        public string FilePath { get; set; }

//        public VideoItem(string fileName, string filePath)
//        {
//            FileName = fileName;
//            FilePath = filePath;
//        }
//    }
//}

namespace ConsumerGUI
{
    public class VideoItem
    {
        public string FileName { get; }
        public string PlaybackUrl { get; }

        public VideoItem(string fileName, string playbackUrl)
        {
            FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
            PlaybackUrl = playbackUrl ?? throw new ArgumentNullException(nameof(playbackUrl));
        }
    }
}
