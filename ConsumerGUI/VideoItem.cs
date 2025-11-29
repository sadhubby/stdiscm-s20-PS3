namespace ConsumerGUI
{
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
