namespace RoRebuildServer.Data.CsvDataTypes;

public class TemporaryFile : IDisposable
{
    public string FilePath;

    public TemporaryFile(string path)
    {
        var tempFileName = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
        File.Copy(path, tempFileName, true);
        FilePath = tempFileName;
    }

    public void Dispose()
    {
        if (string.IsNullOrWhiteSpace(FilePath))
            return;
        File.Delete(FilePath);
    }
}