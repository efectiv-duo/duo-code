using System.Text;
using duo_code.Tools.Core;

namespace duo_code.Tools;

public class ListFilesAction : ToolActionBase
{
    public override string ToolName => "LIST_FILES";
    public override string Description => @"List directory contents. Optional depth (default 1, max 5)
Format:
LIST_FILES: path depth:N";
    
    public string Path { get; set; } = string.Empty;
    public int Depth { get; set; } = 1;
    
    
    protected override string ExecuteCore(string baseDirectory)
    {
        var fullPath = ResolvePath(baseDirectory, Path);
        if (!Directory.Exists(fullPath)) throw new DirectoryNotFoundException($"Directory not found: {Path}");

        var output = new StringBuilder();
        output.AppendLine($"Executed LIST_FILES: {Path} (depth={Depth})");
        
        var rootDir = new DirectoryInfo(fullPath);
        var allItems = new List<string>();
        
        // Collect all items with their relative paths
        CollectItems(rootDir, Path, allItems, 0, Depth);
        
        // Sort and output
        allItems.Sort();
        foreach (var item in allItems)
        {
            output.AppendLine(item);
        }
        
        // Count summary
        var dirCount = allItems.Count(i => i.EndsWith("/"));
        var fileCount = allItems.Count - dirCount;
        output.AppendLine($"\nTotal: {dirCount} dirs, {fileCount} files");

        ConsoleMessage = $"Listed {dirCount} dirs, {fileCount} files";

        return output.ToString();
    }
    
    private void CollectItems(DirectoryInfo dir, string relativePath, List<string> items, int currentDepth, int maxDepth)
    {
        // Get directories and files
        var directories = dir.GetDirectories()
            .Where(d => !ShouldIgnoreDirectory(d, relativePath))
            .OrderBy(d => d.Name);
            
        var files = dir.GetFiles()
            .Where(f => !ShouldIgnoreFile(f, relativePath))
            .OrderBy(f => f.Name);
        
        // Add directories
        foreach (var subDir in directories)
        {
            var dirPath = string.IsNullOrEmpty(relativePath) ? subDir.Name : $"{relativePath}/{subDir.Name}";
            items.Add(dirPath + "/");
            
            // Recurse if we haven't reached max depth
            if (currentDepth < maxDepth - 1)
            {
                CollectItems(subDir, dirPath, items, currentDepth + 1, maxDepth);
            }
        }
        
        // Add files
        foreach (var file in files)
        {
            var filePath = string.IsNullOrEmpty(relativePath) ? file.Name : $"{relativePath}/{file.Name}";
            items.Add(filePath);
        }
    }
    
    private bool ShouldIgnoreDirectory(DirectoryInfo dir, string relativePath)
    {
        // Always ignore .git and other dot directories
        if (dir.Name.StartsWith('.')) return true;
        
        var fullPath = dir.FullName;
        var baseDir = GetBaseDirectory(fullPath, relativePath, dir.Name);
        return GitignoreUtils.ShouldIgnoreFile(fullPath, baseDir);
    }
    
    private bool ShouldIgnoreFile(FileInfo file, string relativePath)
    {
        var fullPath = file.FullName;
        var baseDir = GetBaseDirectory(fullPath, relativePath, file.Name);
        return GitignoreUtils.ShouldIgnoreFile(fullPath, baseDir);
    }
    
    private string GetBaseDirectory(string fullPath, string relativePath, string itemName)
    {
        // Calculate the base directory by removing the relative path portion
        if (string.IsNullOrEmpty(relativePath))
        {
            return System.IO.Path.GetDirectoryName(fullPath) ?? fullPath;
        }
        
        // Remove the relative path and item name to get the base directory
        var pathToRemove = $"{relativePath}/{itemName}".Replace('/', System.IO.Path.DirectorySeparatorChar);
        if (fullPath.EndsWith(pathToRemove))
        {
            return fullPath.Substring(0, fullPath.Length - pathToRemove.Length).TrimEnd(System.IO.Path.DirectorySeparatorChar);
        }
        
        return System.IO.Path.GetDirectoryName(fullPath) ?? fullPath;
    }
    
    protected override string? CreateSummary(string fullResult)
    {
        var lines = fullResult.Split('\n');
        
        // If result is reasonably small, don't summarize
        if (lines.Length <= 30) return null;
        
        // Take first 20 lines and the summary line
        var truncatedLines = new List<string>();
        truncatedLines.AddRange(lines.Take(20));
        
        // Find the total line (usually last non-empty line)
        var totalLine = lines.LastOrDefault(l => l.StartsWith("Total:"));
        if (totalLine != null)
        {
            truncatedLines.Add($"... {lines.Length - 21} more items");
            truncatedLines.Add(totalLine);
        }
        else
        {
            truncatedLines.Add($"... {lines.Length - 20} more items");
        }
        
        return string.Join('\n', truncatedLines);
    }
    
    public override string ToString()
    {
        return Depth > 1 ? $"{ToolName}: {Path} depth:{Depth}" : $"{ToolName}: {Path}";
    }
}