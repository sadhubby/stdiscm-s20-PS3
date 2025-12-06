using System;

namespace ConsumerGUI
{
    //public class VideoItem
    //{
    //    public string FileName { get; }
    //    public string FilePath { get; }

    //    public VideoItem(string fileName, string playbackURL)
    //    {
    //        FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
    //        // Corrected the assignment of FilePath to use playbackURL
    //        FilePath = playbackURL ?? throw new ArgumentNullException(nameof(playbackURL));
    //    }
    //}
    public class VideoItem
    {
        public string FileName { get; set; }
        public string FilePath { get; set; }

        public VideoItem(string fileName, string filePath)
        {
            FileName = fileName;
            FilePath = filePath;
        }
    }
}