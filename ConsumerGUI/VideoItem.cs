namespace ConsumerGUI
{
    public class VideoItem
    {
        public string FileName { get; }
        public string FilePath { get; }

        public VideoItem(string fileName, string playbackURL)
        {
            FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        }
    }
}
