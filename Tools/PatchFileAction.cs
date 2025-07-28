using System.Text;
using System.Text.RegularExpressions;
using duo_code.Tools.Core;

namespace duo_code.Tools;

public class PatchFileAction : ToolActionBase
{
    public override string ToolName => "PATCH_FILE";
    public override string Description => @"Apply targeted changes to files with smart matching and error recovery.

SIMPLE FORMAT (recommended):
PATCH_FILE: file_path
<<<< FIND
content to replace (exact match)
>>>>
<<<<< REPLACE
new content
>>>>>

ADVANCED FORMAT:
PATCH_FILE: file_path  
<<<< FIND_FUZZY
partial content to match (allows whitespace differences)
>>>>
<<<<< REPLACE
new content
>>>>>

MULTIPLE CHANGES:
PATCH_FILE: file_path
<<<< FIND
first content
>>>>
<<<<< REPLACE
first replacement
>>>>>
<<<< FIND
second content  
>>>>
<<<<< REPLACE
second replacement
>>>>>";
    
    public string Path { get; set; } = string.Empty;
    public List<SmartPatchOperation> Patches { get; set; } = new();
    
    protected override string ExecuteCore(string baseDirectory)
    {
        var fullPath = ResolvePath(baseDirectory, Path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"File not found: {Path}");
        }
        
        // Read current content
        var content = File.ReadAllText(fullPath);
        var lines = content.Split('\n');
        var appliedPatches = 0;
        var modifications = new List<string>();
        
        // Apply each patch
        foreach (var patch in Patches)
        {
            var result = ApplySmartPatch(content, patch);
            if (result.Success)
            {
                content = result.ModifiedContent;
                appliedPatches++;
                modifications.Add($"✓ {result.Description}");
            }
            else
            {
                modifications.Add($"✗ {result.ErrorMessage}");
                throw new InvalidOperationException($"Patch failed: {result.ErrorMessage}\n\nSuggestion: {result.Suggestion}");
            }
        }
        
        // Write the modified content back
        File.WriteAllText(fullPath, content);
        
        // Store detailed result
        var resultMessage = $"Successfully patched {Path}:\n" + string.Join("\n", modifications);
        FullResult = resultMessage;
        
        return $"Patched {Path} - {appliedPatches} changes applied";
    }
    
    private PatchResult ApplySmartPatch(string content, SmartPatchOperation patch)
    {
        try
        {
            switch (patch.MatchType)
            {
                case PatchMatchType.Exact:
                    return ApplyExactMatch(content, patch);
                    
                case PatchMatchType.Fuzzy:
                    return ApplyFuzzyMatch(content, patch);
                    
                case PatchMatchType.Regex:
                    return ApplyRegexMatch(content, patch);
                    
                default:
                    return PatchResult.CreateFailure("Unknown patch match type", "Use FIND, FIND_FUZZY, or FIND_REGEX");
            }
        }
        catch (Exception ex)
        {
            return PatchResult.CreateFailure($"Patch error: {ex.Message}", "Check your patch syntax and content");
        }
    }
    
    private PatchResult ApplyExactMatch(string content, SmartPatchOperation patch)
    {
        if (!content.Contains(patch.FindContent))
        {
            // Try to provide helpful suggestions
            var suggestion = GetSuggestionForMissingContent(content, patch.FindContent);
            return PatchResult.CreateFailure(
                $"Exact content not found: '{TruncateForDisplay(patch.FindContent)}'",
                suggestion);
        }
        
        var modifiedContent = content.Replace(patch.FindContent, patch.ReplaceContent);
        var description = $"Exact match: replaced '{TruncateForDisplay(patch.FindContent)}' with '{TruncateForDisplay(patch.ReplaceContent)}'";
        
        return PatchResult.CreateSuccess(modifiedContent, description);
    }
    
    private PatchResult ApplyFuzzyMatch(string content, SmartPatchOperation patch)
    {
        // Normalize whitespace for comparison
        var normalizedFind = NormalizeWhitespace(patch.FindContent);
        var contentLines = content.Split('\n');
        
        // Try to find a fuzzy match
        for (int i = 0; i <= contentLines.Length - normalizedFind.Split('\n').Length; i++)
        {
            var candidateLines = contentLines.Skip(i).Take(normalizedFind.Split('\n').Length);
            var candidateContent = string.Join("\n", candidateLines);
            var normalizedCandidate = NormalizeWhitespace(candidateContent);
            
            if (normalizedCandidate == normalizedFind)
            {
                // Found a match - replace the original (non-normalized) content
                var originalMatch = string.Join("\n", candidateLines);
                var modifiedContent = content.Replace(originalMatch, patch.ReplaceContent);
                var description = $"Fuzzy match: replaced content around line {i + 1}";
                
                return PatchResult.CreateSuccess(modifiedContent, description);
            }
        }
        
        var suggestion = GetSuggestionForMissingContent(content, patch.FindContent);
        return PatchResult.CreateFailure(
            $"Fuzzy content not found: '{TruncateForDisplay(patch.FindContent)}'",
            suggestion);
    }
    
    private PatchResult ApplyRegexMatch(string content, SmartPatchOperation patch)
    {
        try
        {
            var regex = new Regex(patch.FindContent, RegexOptions.Multiline | RegexOptions.Singleline);
            if (!regex.IsMatch(content))
            {
                return PatchResult.CreateFailure(
                    $"Regex pattern not found: '{TruncateForDisplay(patch.FindContent)}'",
                    "Check your regular expression syntax and ensure it matches the target content");
            }
            
            var modifiedContent = regex.Replace(content, patch.ReplaceContent);
            var description = $"Regex match: applied pattern '{TruncateForDisplay(patch.FindContent)}'";
            
            return PatchResult.CreateSuccess(modifiedContent, description);
        }
        catch (ArgumentException ex)
        {
            return PatchResult.CreateFailure($"Invalid regex pattern: {ex.Message}", "Use valid .NET regular expression syntax");
        }
    }
    
    private string NormalizeWhitespace(string input)
    {
        // Normalize line endings and collapse multiple whitespace
        return Regex.Replace(input.Replace("\r\n", "\n").Replace("\r", "\n"), @"\s+", " ").Trim();
    }
    
    private string GetSuggestionForMissingContent(string content, string findContent)
    {
        // Find similar content using simple string similarity
        var findWords = findContent.Split(new[] { ' ', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        var contentLines = content.Split('\n');
        
        var bestMatch = "";
        var bestScore = 0.0;
        
        foreach (var line in contentLines)
        {
            var score = CalculateSimilarity(findContent, line);
            if (score > bestScore && score > 0.3) // Minimum similarity threshold
            {
                bestScore = score;
                bestMatch = line.Trim();
            }
        }
        
        if (!string.IsNullOrEmpty(bestMatch))
        {
            return $"Did you mean: '{TruncateForDisplay(bestMatch)}'? Try using FIND_FUZZY for flexible matching.";
        }
        
        return "Consider using FIND_FUZZY for flexible matching, or check the exact content in the file.";
    }
    
    private double CalculateSimilarity(string source, string target)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
            return 0.0;
            
        var sourceWords = source.ToLower().Split(new[] { ' ', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        var targetWords = target.ToLower().Split(new[] { ' ', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        
        var commonWords = sourceWords.Intersect(targetWords).Count();
        var totalWords = Math.Max(sourceWords.Length, targetWords.Length);
        
        return totalWords > 0 ? (double)commonWords / totalWords : 0.0;
    }
    
    private string TruncateForDisplay(string text, int maxLength = 100)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;
            
        return text.Substring(0, maxLength) + "...";
    }
    
    public override string ToString()
    {
        return $"{ToolName}: {Path}";
    }
}

public class SmartPatchOperation
{
    public PatchMatchType MatchType { get; set; }
    public string FindContent { get; set; } = string.Empty;
    public string ReplaceContent { get; set; } = string.Empty;
}

public enum PatchMatchType
{
    Exact,    // FIND - exact string match
    Fuzzy,    // FIND_FUZZY - whitespace-insensitive match
    Regex     // FIND_REGEX - regular expression match
}

public class PatchResult
{
    public bool Success { get; set; }
    public string ModifiedContent { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public string Suggestion { get; set; } = string.Empty;
    
    public static PatchResult CreateSuccess(string modifiedContent, string description)
    {
        return new PatchResult
        {
            Success = true,
            ModifiedContent = modifiedContent,
            Description = description
        };
    }
    
    public static PatchResult CreateFailure(string errorMessage, string suggestion)
    {
        return new PatchResult
        {
            Success = false,
            ErrorMessage = errorMessage,
            Suggestion = suggestion
        };
    }
}