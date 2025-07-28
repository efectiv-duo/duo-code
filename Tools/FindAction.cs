using System.Text;
using duo_code.Tools.Core;

namespace duo_code.Tools;

public class FindAction : ToolActionBase
{
    public override string ToolName => "FIND";
    public override string Description => @"Find files recursively. Simple wildcards: * and ?
Examples: *.cs, **/PatchFile*.cs, Test*.js
Format:
FIND: pattern";
    
    public string Pattern { get; set; } = string.Empty;
    
    
    protected override string ExecuteCore(string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(Pattern))
            return "Error: Pattern cannot be empty";

        var results = new List<string>();
        
        try
        {
            // Get all files recursively, respecting .gitignore
            var allFiles = Directory.EnumerateFiles(baseDirectory, "*", SearchOption.AllDirectories)
                .Where(f => !GitignoreUtils.ShouldIgnoreFile(f, baseDirectory));

            foreach (var file in allFiles)
            {
                var relativePath = Path.GetRelativePath(baseDirectory, file).Replace('\\', '/');
                if (MatchesPattern(relativePath, Pattern))
                {
                    results.Add(relativePath);
                }
            }
        }
        catch (Exception ex)
        {
            return $"Error searching: {ex.Message}";
        }

        results.Sort();
        
        var output = new StringBuilder();
        output.AppendLine($"Executed FIND: {Pattern}");
        
        if (results.Count == 0)
        {
            output.AppendLine("No matches found");
        }
        else
        {
            var showCount = Math.Min(results.Count, 150);
            for (int i = 0; i < showCount; i++)
            {
                output.AppendLine(results[i]);
            }
            
            if (results.Count > 150)
            {
                output.AppendLine($"... +{results.Count - 150} more results");
            }
        }
        
        output.AppendLine($"\nFound {results.Count} files");
        return output.ToString();
    }
    
    
    private bool MatchesPattern(string path, string pattern)
    {
        // Handle ** (recursive directory wildcard)
        if (pattern.Contains("**/"))
        {
            var parts = pattern.Split(new[] { "**/" }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
            {
                // Pattern like "**/filename.cs" 
                return MatchesSimplePattern(Path.GetFileName(path), parts[0]);
            }
            else if (parts.Length == 2)
            {
                // Pattern like "dir/**/filename.cs"
                return path.StartsWith(parts[0]) && MatchesSimplePattern(Path.GetFileName(path), parts[1]);
            }
        }
        
        // Simple pattern matching (no directory separators)
        if (!pattern.Contains('/'))
        {
            return MatchesSimplePattern(Path.GetFileName(path), pattern);
        }
        
        // Full path pattern matching
        return MatchesSimplePattern(path, pattern);
    }
    
    private bool MatchesSimplePattern(string text, string pattern)
    {
        // Convert simple glob pattern to regex
        var regexPattern = "^" + pattern
            .Replace(".", "\\.")
            .Replace("*", ".*")
            .Replace("?", ".") + "$";
        
        return System.Text.RegularExpressions.Regex.IsMatch(text, regexPattern, 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
    
    public override string ToString()
    {
        return $"{ToolName}: {Pattern}";
    }
}