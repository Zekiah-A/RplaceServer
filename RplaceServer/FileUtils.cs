using System.Collections;
using System.Reflection;

namespace RplaceServer;

public static class FileUtils
{
    public static void RecursiveCopy(string sourceDir, string targetDir, bool overwrite = false)
    {
        Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destinationFile = Path.Combine(targetDir, Path.GetFileName(file));
            if (File.Exists(destinationFile) && !overwrite)
            {
                continue;
            }
            
            File.Copy(file, destinationFile, overwrite);
        }

        foreach (var directory in Directory.GetDirectories(sourceDir))
        {
            RecursiveCopy(directory, Path.Combine(targetDir, Path.GetFileName(directory)));
        }
    }
    
    /// <summary>
    /// These are plaintext files, made up of a string list separated by a \n, but also can contain
    /// links to other hosted plaintext files, which will also be evaluated and added to the dictionary
    /// </summary>
    public static async Task ReadUrlSheet<T>(HttpClient httpClient, IEnumerable<string> text, T targetList) where T : IList
    {
        foreach (var line in text)
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
            {
                continue;
            }

            var sections = line.Split(" ");
            switch (sections.Length)
            {
                case 0:
                {
                    continue;
                }
                case 1:
                {
                    if (Uri.TryCreate(sections[0], UriKind.Absolute, out var sheetUri) &&
                        (sheetUri.Scheme == Uri.UriSchemeHttp || sheetUri.Scheme == Uri.UriSchemeHttps))
                    {
                        var response = await httpClient.GetAsync(sections[0]);
                        if (response.IsSuccessStatusCode)
                        {
                            var linkSheet = await response.Content.ReadAsStringAsync();
                            await ReadUrlSheet(httpClient, linkSheet.Split("\n"), targetList);
                        }
                    }
                    else
                    {
                        targetList.Add(sections[0]);
                    }
                    break;
                }
            }
        }
    }


    public static void ReadListFile(IEnumerable<string> text, List<string> targetList)
    {
        foreach (var line in text)
        {
            var entry = line.Trim();
            if (string.IsNullOrWhiteSpace(entry) || entry.StartsWith('#'))
            {
                continue;
            }
            
            targetList.Add(entry);
        }
    }
}