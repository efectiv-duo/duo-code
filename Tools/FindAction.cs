using System.Text;
using duo_code.Tools.Core;

namespace duo_code.Tools;

public class FindAction : ToolActionBase
{
    public override string ToolName => "FIND";
    public override string Description => @"Search files or directories recursively. Wildcards: * ?
Format:
FIND: pattern";
    
    public string Pattern { get; set; } = string.Empty;
    
    // Common directories to ignore
    private static readonly HashSet<string> IgnoredDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", "vendor", "packages", "bin", "obj",
        "dist", "build", "out", "target", "__pycache__", 
        "venv", "env", "bower_components", "jspm_packages", 
        "pkg", "Pods", "deps", "_build"
    };
    
    protected override string ExecuteCore(string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(Pattern))
            throw new ArgumentException("Search pattern cannot be empty");

        var output = new StringBuilder();
        output.AppendLine($"FIND: {Pattern}");
        
        var rootDir = new DirectoryInfo(baseDirectory);
        var results = new List<string>();
        
        // Search recursively
        SearchDirectory(rootDir, rootDir.FullName, Pattern, results);
        
        // Sort results for consistency
        results.Sort();
        
        // Output results
        if (results.Count == 0)
        {
            output.AppendLine("No matches found");
        }
        else
        {
            const int maxResults = 300;
            var resultsToShow = Math.Min(results.Count, maxResults);
            
            for (int i = 0; i < resultsToShow; i++)
            {
                output.AppendLine(results[i]);
            }
            
            if (results.Count > maxResults)
            {
                output.AppendLine($"\n... +{results.Count - maxResults} more results");
            }
        }
        
        // Summary
        var dirCount = results.Count(r => r.EndsWith("/"));
        var fileCount = results.Count - dirCount;
        output.AppendLine($"\nFound: {dirCount} dirs, {fileCount} files");
        
        return output.ToString();
    }
    
    private void SearchDirectory(DirectoryInfo dir, string rootPath, string pattern, List<string> results)
    {
        try
        {
            // Search for matching files in current directory
            var matchingFiles = dir.GetFiles(pattern);
            foreach (var file in matchingFiles)
            {
                var relativePath = GetRelativePath(rootPath, file.FullName);
                results.Add(relativePath);
            }
            
            // Search for matching directories
            var matchingDirs = dir.GetDirectories(pattern)
                .Where(d => !ShouldIgnoreDirectory(d.Name));
            foreach (var matchingDir in matchingDirs)
            {
                var relativePath = GetRelativePath(rootPath, matchingDir.FullName);
                results.Add(relativePath + "/");
            }
            
            // Recursively search subdirectories
            var subDirs = dir.GetDirectories()
                .Where(d => !ShouldIgnoreDirectory(d.Name));
            foreach (var subDir in subDirs)
            {
                SearchDirectory(subDir, rootPath, pattern, results);
            }
        }
        catch
        {
            // Ignore access errors
        }
    }
    
    private bool ShouldIgnoreDirectory(string dirName)
    {
        return dirName.StartsWith('.') || IgnoredDirs.Contains(dirName);
    }
    
    private string GetRelativePath(string rootPath, string fullPath)
    {
        var relativePath = Path.GetRelativePath(rootPath, fullPath);
        return relativePath.Replace('\\', '/'); // Use forward slashes for consistency
    }
    
    protected override string? CreateSummary(string fullResult)
    {
        var lines = fullResult.Split('\n');
        
        // If result is reasonably small, don't summarize
        if (lines.Length <= 30) return null;
        
        // Take first 20 lines and the summary
        var truncatedLines = new List<string>();
        truncatedLines.AddRange(lines.Take(20));
        
        // Find the Found line (usually near the end)
        var foundLine = lines.LastOrDefault(l => l.StartsWith("Found:"));
        if (foundLine != null)
        {
            truncatedLines.Add($"... {lines.Length - 21} more results");
            truncatedLines.Add(foundLine);
        }
        else
        {
            truncatedLines.Add($"... {lines.Length - 20} more results");
        }
        
        return string.Join('\n', truncatedLines);
    }
    
    public override string ToString()
    {
        return $"{ToolName}: {Pattern}";
    }
}