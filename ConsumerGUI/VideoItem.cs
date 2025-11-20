public class VideoItem
{
    public string FileName { get; }
    public string FilePath { get; }

    public VideoItem(string fileName, string filePath)
    {
        FileName = fileName;
        FilePath = filePath;
    }
}
