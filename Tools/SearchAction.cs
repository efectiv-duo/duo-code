using System.Text;
using System.Text.RegularExpressions;
using duo_code.Tools.Core;

namespace duo_code.Tools;

public class SearchAction : ToolActionBase
{
    public override string ToolName => "SEARCH";
    public override string Description => @"Search for text inside files recursively. Respects .gitignore and supports regex.
Format:
SEARCH: search_term
SEARCH: regex:/pattern/flags
SEARCH: case:search_term (case-sensitive)
SEARCH: context:N:search_term (N lines of context before/after match)";
    
    public string Pattern { get; set; } = string.Empty;
    
    private static readonly string[] TextExtensions = [
        ".cs", ".js", ".ts", ".jsx", ".tsx", ".html", ".css", ".scss", ".json", ".xml", ".txt", ".md", 
        ".yml", ".yaml", ".config", ".sql", ".py", ".java", ".cpp", ".h", ".c", ".go", ".rs", ".php",
        ".rb", ".sh", ".bat", ".ps1", ".vue", ".svelte", ".dart", ".kt", ".swift", ".scala"
    ];
    
    protected override string ExecuteCore(string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(Pattern))
            return "Error: Search term cannot be empty";

        var (searchPattern, isRegex, isCaseSensitive, contextLines) = ParseSearchPattern(Pattern);
        var results = new List<string>();
        var warnings = new List<string>();
        
        try
        {
            var allFiles = Directory.EnumerateFiles(baseDirectory, "*", SearchOption.AllDirectories)
                .Where(f => !GitignoreUtils.ShouldIgnoreFile(f, baseDirectory) && IsTextFile(f));

            foreach (var file in allFiles)
            {
                SearchInFile(file, baseDirectory, searchPattern, isRegex, isCaseSensitive, contextLines, results, warnings);
            }
        }
        catch (Exception ex)
        {
            return $"Error searching: {ex.Message}";
        }

        var output = new StringBuilder();
        output.AppendLine($"Executed SEARCH: {Pattern}");
        
        // Show any warnings
        foreach (var warning in warnings.Distinct())
        {
            output.AppendLine($"Warning: {warning}");
        }
        if (warnings.Count > 0) output.AppendLine();
        
        if (results.Count == 0)
        {
            output.AppendLine("No matches found");
        }
        else
        {
            var showCount = Math.Min(results.Count, 100);
            for (int i = 0; i < showCount; i++)
            {
                output.AppendLine(results[i]);
            }
            
            if (results.Count > 100)
            {
                output.AppendLine($"... +{results.Count - 100} more results");
            }
        }
        
        output.AppendLine($"\nFound {results.Count} matches");
        return output.ToString();
    }
    
    private (string pattern, bool isRegex, bool isCaseSensitive, int contextLines) ParseSearchPattern(string input)
    {
        if (input.StartsWith("context:"))
        {
            var parts = input.Substring(8).Split(':', 2);
            if (parts.Length == 2 && int.TryParse(parts[0], out int context))
            {
                return (parts[1], false, false, Math.Min(context, 10)); // Max 10 lines context
            }
        }
        if (input.StartsWith("regex:"))
        {
            return (input.Substring(6), true, false, 0);
        }
        if (input.StartsWith("case:"))
        {
            return (input.Substring(5), false, true, 0);
        }
        return (input, false, false, 0);
    }
    
    
    private void SearchInFile(string filePath, string baseDirectory, string searchPattern, bool isRegex, bool isCaseSensitive, int contextLines, List<string> results, List<string> warnings)
    {
        try
        {
            var relativePath = Path.GetRelativePath(baseDirectory, filePath).Replace('\\', '/');
            
            // Read all lines for context support
            var allLines = File.ReadAllLines(filePath, Encoding.UTF8);
            bool regexFallbackWarned = false;
            
            for (int lineNumber = 0; lineNumber < allLines.Length; lineNumber++)
            {
                var line = allLines[lineNumber];
                bool isMatch = false;
                
                if (isRegex)
                {
                    try
                    {
                        var options = isCaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                        isMatch = Regex.IsMatch(line, searchPattern, options);
                    }
                    catch
                    {
                        // Invalid regex - fall back to literal search
                        if (!regexFallbackWarned)
                        {
                            warnings.Add("Invalid regex provided. Falling back to literal search.");
                            regexFallbackWarned = true;
                        }
                        var comparison = isCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                        isMatch = line.Contains(searchPattern, comparison);
                    }
                }
                else
                {
                    var comparison = isCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                    isMatch = line.Contains(searchPattern, comparison);
                }
                
                if (isMatch)
                {
                    if (contextLines > 0)
                    {
                        // Add context lines
                        var contextResult = new StringBuilder();
                        var startLine = Math.Max(0, lineNumber - contextLines);
                        var endLine = Math.Min(allLines.Length - 1, lineNumber + contextLines);
                        
                        for (int i = startLine; i <= endLine; i++)
                        {
                            var prefix = i == lineNumber ? ">" : " ";
                            contextResult.AppendLine($"{relativePath}:{i + 1}:{prefix} {allLines[i].Trim()}");
                        }
                        
                        results.Add(contextResult.ToString().TrimEnd());
                    }
                    else
                    {
                        var trimmedLine = line.Trim();
                        results.Add($"{relativePath}:{lineNumber + 1}: {trimmedLine}");
                    }
                }
            }
        }
        catch
        {
            // Ignore files that can't be read (binary, permission issues, etc.)
        }
    }
    
    private bool IsTextFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return TextExtensions.Contains(extension) || string.IsNullOrEmpty(extension);
    }
    
    public override string ToString()
    {
        return $"{ToolName}: {Pattern}";
    }
}