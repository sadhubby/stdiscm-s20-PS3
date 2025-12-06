using System;

namespace ConsumerGUI
{
    public class VideoItem
    {
        public string FileName { get; }
        public string FilePath { get; }

        public VideoItem(string fileName, string playbackURL)
        {
            FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
            // Corrected the assignment of FilePath to use playbackURL
            FilePath = playbackURL ?? throw new ArgumentNullException(nameof(playbackURL)); 
        }
    }
}