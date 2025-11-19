public class VideoItem
{
    public string FileName { get; }
    public string FilePath { get; }

    public VideoItem(string name, string path)
    {
        FileName = name;
        FilePath = path;
    }

    public override string ToString()
    {
        return FileName;
    }
}
