namespace Watchdog.Server.Lib;

public static class AtomicFile
{
    private const int MaxAttempts = 5;

    public static void WriteAllText(string path, string content)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var tempPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
        File.WriteAllText(tempPath, content);
        CommitTempFile(tempPath, path);
    }

    public static void Copy(string sourcePath, string destinationPath)
    {
        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var tempPath = destinationPath + ".tmp-" + Guid.NewGuid().ToString("N");
        File.Copy(sourcePath, tempPath, overwrite: true);

        CommitTempFile(tempPath, destinationPath);
    }

    private static void CommitTempFile(string tempPath, string destinationPath)
    {
        IOException? lastError = null;
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                if (File.Exists(destinationPath))
                {
                    File.Replace(tempPath, destinationPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
                    return;
                }

                File.Move(tempPath, destinationPath);
                return;
            }
            catch (IOException) when (attempt < MaxAttempts)
            {
                lastError = null;
                Thread.Sleep(50 * attempt);
            }
            catch (IOException ex)
            {
                lastError = ex;
                break;
            }
        }

        if (File.Exists(tempPath))
            File.Delete(tempPath);

        throw lastError ?? new IOException($"Could not commit atomic file to {destinationPath}.");
    }
}