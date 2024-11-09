using System.Collections;
using System.Reflection;
using System.Text.Json;

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
            if (sections.Length == 0)
            {
                continue;
            }
            if (sections.Length == 1)
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
            }
        }
    }

    public static async Task ReadJsonMapFile<T>(string path, Dictionary<string, T> targetDictionary, JsonSerializerOptions jsonOptions)
    {
        await using var file = File.OpenRead(path);
        using var reader = new StreamReader(file);
        var line = await reader.ReadLineAsync();
        while (line is not null)
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
            {
                continue;
            }

            var sections = line.Split(" ");
            if (sections.Length > 2)
            {
                continue;
            }
            var valueObject = JsonSerializer.Deserialize<T>(sections[1], jsonOptions);
            if (valueObject == null)
            {
                return;
            }
            targetDictionary.Add(sections[0], valueObject);
            
            line = await reader.ReadLineAsync();
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