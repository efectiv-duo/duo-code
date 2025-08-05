using duo_code.Tools.Core;
using System.Text;

namespace duo_code.Tools;

public class ListFilesAction : ToolActionBase
{
    public override string ToolName => "LIST_FILES";
    public override string Description => @"List directory contents with metadata. Optional depth (default 1, max 5) and directories_only flag
Format:
LIST_FILES: path depth:N directories_only:true/false";

    public string Path { get; set; } = string.Empty;
    public int Depth { get; set; } = 1;
    public bool DirectoriesOnly { get; set; } = false;

    private const int MaxDepth = 5;
    private const int MaxFiles = 10000;
    private const long MaxTotalSize = 1L * 1024 * 1024 * 1024; // 1GB

    private int _fileCount;
    private long _totalSize;

    protected override string ExecuteCore(string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(Path))
            Path = ".";

        if (Depth < 1)
            return "Error: Depth must be at least 1";

        if (Depth > MaxDepth)
            return $"Error: Depth cannot exceed {MaxDepth}";

        var fullPath = ResolvePath(baseDirectory, Path);

        if (!Directory.Exists(fullPath))
            return $"Error: Directory not found: {Path}";

        try
        {
            var output = new StringBuilder();
            output.AppendLine($"LISTING: {Path} depth: {Depth}");
            output.AppendLine($"[META] Base: {fullPath}");
            output.AppendLine();

            _fileCount = 0;
            _totalSize = 0;

            var rootDir = new DirectoryInfo(fullPath);
            var filesByDirectory = new Dictionary<string, List<(string name, long size, DateTime modified)>>();
            var directoriesInfo = new Dictionary<string, (int fileCount, int dirCount)>();
            var extensionsByDirectory = new Dictionary<string, Dictionary<string, int>>();

            CollectItemsGrouped(rootDir, "", filesByDirectory, directoriesInfo, extensionsByDirectory, 0, Depth);

            if (_fileCount >= MaxFiles || _totalSize >= MaxTotalSize)
            {
                output.AppendLine($"[WARNING] Output truncated: {_fileCount} files, {FormatSize(_totalSize)}");
                output.AppendLine();
            }

            var sortedPaths = filesByDirectory.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();

            foreach (var dirPath in sortedPaths)
            {
                var files = filesByDirectory[dirPath];
                var displayPath = string.IsNullOrEmpty(dirPath) ? "ROOT" : dirPath;

                var header = $"[{displayPath}: {files.Count} files";
                if (directoriesInfo.ContainsKey(dirPath))
                {
                    var (_, dirCount) = directoriesInfo[dirPath];
                    if (dirCount > 0)
                        header += $", {dirCount} dirs";
                }

                if (DirectoriesOnly && extensionsByDirectory.ContainsKey(dirPath) && extensionsByDirectory[dirPath].Count > 0)
                {
                    var extensions = extensionsByDirectory[dirPath]
                        .OrderByDescending(kvp => kvp.Value)
                        .ThenBy(kvp => kvp.Key)
                        .Select(kvp => $"{kvp.Key}:{kvp.Value}");
                    header += $"] {{{string.Join(", ", extensions)}}}";
                }
                else
                {
                    header += "]";
                }

                output.AppendLine(header);

                if (!DirectoriesOnly)
                {
                    foreach (var (name, size, modified) in files.OrderBy(f => f.name, StringComparer.Ordinal))
                    {
                        output.AppendLine($"{name} [{FormatSize(size)}] [{GetActivityMarker(modified)}]");
                    }
                }

                output.AppendLine();
            }

            output.AppendLine("[SUMMARY]");
            var totalDirs = directoriesInfo.Values.Sum(v => v.dirCount);
            output.AppendLine($"Total: {_fileCount} files, {totalDirs} directories ({FormatSize(_totalSize)})");

            ConsoleMessage = $"Listed {totalDirs} dirs, {_fileCount} files ({FormatSize(_totalSize)})";

            return output.ToString();
        }
        catch (UnauthorizedAccessException)
        {
            return $"Error: Access denied to directory: {Path}";
        }
        catch (Exception ex)
        {
            return $"Error listing directory: {ex.Message}";
        }
    }

    private void CollectItemsGrouped(
        DirectoryInfo dir,
        string relativePath,
        Dictionary<string, List<(string name, long size, DateTime modified)>> filesByDirectory,
        Dictionary<string, (int fileCount, int dirCount)> directoriesInfo,
        Dictionary<string, Dictionary<string, int>> extensionsByDirectory,
        int currentDepth,
        int maxDepth)
    {
        if (_fileCount >= MaxFiles || _totalSize >= MaxTotalSize) return;

        try
        {
            var directories = dir.GetDirectories()
                .Where(d => !ShouldIgnoreDirectory(d))
                .OrderBy(d => d.Name)
                .ToList();

            var fileInfos = dir.GetFiles()
                .Where(f => !ShouldIgnoreFile(f))
                .OrderBy(f => f.Name)
                .ToList();

            if (fileInfos.Count > 0 || currentDepth == 0)
            {
                filesByDirectory[relativePath] = new List<(string, long, DateTime)>();
                extensionsByDirectory[relativePath] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                foreach (var file in fileInfos)
                {
                    if (_fileCount >= MaxFiles || _totalSize >= MaxTotalSize) break;

                    _fileCount++;
                    _totalSize += file.Length;
                    filesByDirectory[relativePath].Add((file.Name, file.Length, file.LastWriteTime));

                    var extension = GetFileExtensionOrType(file.Name);
                    extensionsByDirectory[relativePath].TryGetValue(extension, out var count);
                    extensionsByDirectory[relativePath][extension] = count + 1;
                }
            }

            directoriesInfo.TryGetValue(relativePath, out var dirInfo);
            directoriesInfo[relativePath] = (dirInfo.fileCount + fileInfos.Count, dirInfo.dirCount + directories.Count);

            if (currentDepth < maxDepth - 1)
            {
                foreach (var subDir in directories)
                {
                    if (_fileCount >= MaxFiles || _totalSize >= MaxTotalSize) break;

                    var dirPath = BuildPath(relativePath, subDir.Name);
                    CollectItemsGrouped(subDir, dirPath, filesByDirectory, directoriesInfo, extensionsByDirectory, currentDepth + 1, maxDepth);
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            filesByDirectory.TryAdd(relativePath, new List<(string, long, DateTime)>());
            filesByDirectory[relativePath].Add(("[Access Denied]", 0, DateTime.MinValue));
        }
        catch
        {
            // Silently ignore other errors in subdirectories
        }
    }

    private static bool IsIgnored(FileSystemInfo info)
    {
        return info switch
        {
            DirectoryInfo dir => ShouldIgnoreDirectory(dir),
            FileInfo file => ShouldIgnoreFile(file),
            _ => info.Name.StartsWith('.')
        };
    }

    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", "bin", "obj", "dist", "build", "out", ".git", ".vs", ".idea"
    };

    private static bool ShouldIgnoreDirectory(DirectoryInfo dir)
    {
        return dir.Name.StartsWith('.') ||
               IgnoredDirectories.Contains(dir.Name) ||
               GitignoreUtils.ShouldIgnoreFile(dir.FullName, dir.Parent?.FullName ?? dir.FullName);
    }

    private static bool ShouldIgnoreFile(FileInfo file)
    {
        return file.Name.StartsWith('.') ||
               GitignoreUtils.ShouldIgnoreFile(file.FullName, file.DirectoryName ?? file.FullName);
    }

    private static string FormatSize(long bytes)
    {
        if (bytes == 0) return "0B";

        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        var order = (int)Math.Floor(Math.Log(bytes, 1024));
        if (order >= sizes.Length) order = sizes.Length - 1;

        var size = bytes / Math.Pow(1024, order);
        return order == 0 ? $"{size:0}{sizes[order]}" : $"{size:0.#}{sizes[order]}";
    }

    private static string GetFileExtensionOrType(string fileName)
    {
        var extension = System.IO.Path.GetExtension(fileName).ToLowerInvariant();

        if (string.IsNullOrEmpty(extension))
        {
            return fileName.ToLowerInvariant() switch
            {
                "makefile" or "dockerfile" or "license" or "readme" => fileName.ToLowerInvariant(),
                _ => "no-ext"
            };
        }

        if (fileName.Contains(".test.") || fileName.Contains(".spec."))
            return extension + "-test";
        if (fileName.Contains(".min."))
            return extension + "-min";
        if (fileName.EndsWith(".d.ts"))
            return ".d.ts";

        return extension;
    }

    private static string GetActivityMarker(DateTime lastModified)
    {
        var now = DateTime.Now;
        var age = now - lastModified;

        return age.TotalHours switch
        {
            < 1 => "just now",
            < 24 => "today",
            _ => age.TotalDays switch
            {
                < 2 => "yesterday",
                < 7 => "this week",
                < 30 => "this month",
                < 365 => $"{(int)(age.TotalDays / 30)} months ago",
                _ => $"{(int)(age.TotalDays / 365)} years ago"
            }
        };
    }

    protected override string? CreateSummary(string fullResult)
    {
        var lines = fullResult.Split('\n');

        if (lines.Length <= 50) return null;

        var result = new StringBuilder();
        var inContent = false;
        var contentLineCount = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            if (line.StartsWith('[') && !line.StartsWith("[SUMMARY]") && !line.StartsWith("[META]") && !line.StartsWith("[WARNING]"))
                inContent = true;
            else if (line.StartsWith("[SUMMARY]"))
                inContent = false;

            if (!inContent || contentLineCount < 30)
            {
                result.AppendLine(line);
                if (inContent) contentLineCount++;
            }
            else if (contentLineCount == 30)
            {
                result.AppendLine("... [OUTPUT TRUNCATED] ...");
                contentLineCount++;
            }
        }

        return result.ToString();
    }

    private static string BuildPath(string parent, string child)
    {
        return string.IsNullOrEmpty(parent) ? child : $"{parent}/{child}";
    }

    public override string ToString()
    {
        var parts = new List<string> { $"{ToolName}: {Path}" };
        if (Depth > 1) parts.Add($"depth:{Depth}");
        if (DirectoriesOnly) parts.Add("directories_only:true");
        return string.Join(" ", parts);
    }
}