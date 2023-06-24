using System.Reflection;

namespace RplaceServer;

public static class FileUtils
{
    public static string BuildContentPath;

    static FileUtils()
    {
        BuildContentPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? Directory.GetCurrentDirectory();
    }
    
    public static void RecursiveCopy(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)));
        }

        foreach (var directory in Directory.GetDirectories(sourceDir))
        {
            RecursiveCopy(directory, Path.Combine(targetDir, Path.GetFileName(directory)));
        }
    }
}