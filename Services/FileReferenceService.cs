using System.Text;
using System.Text.RegularExpressions;
using duo_code.Models;
using duo_code.Services.Interfaces;

namespace duo_code.Services;

public class FileReferenceService : IFileReferenceService
{
    private static readonly Regex FileReferenceRegex = new(
        @"@(?<path>[^\s:]+)(?::(?<startLine>\d+)(?:-(?<endLine>\d+))?)?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );
    
    private readonly FileSearchService _fileSearchService;

    public FileReferenceService()
    {
        _fileSearchService = new FileSearchService();
    }

    public async Task<FileReferenceResult> ProcessFileReferencesAsync(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return new FileReferenceResult { ProcessedUserContent = input, HasFileReferences = false };

        var matches = FileReferenceRegex.Matches(input);
        if (matches.Count == 0)
            return new FileReferenceResult { ProcessedUserContent = input, HasFileReferences = false };

        var userContent = new StringBuilder(input);
        var systemContentParts = new List<string>();
        var offset = 0;

        foreach (Match match in matches)
        {
            try
            {
                var fileContent = await ProcessSingleFileReferenceAsync(match);
                var systemMessagePart = BuildSystemMessage(match, fileContent);
                systemContentParts.Add(systemMessagePart);
                
                // Replace the @reference with a simple reference placeholder in user content
                var replacement = GetUserContentReplacement(match);
                var originalLength = match.Value.Length;
                var newLength = replacement.Length;
                
                userContent.Remove(match.Index + offset, originalLength);
                userContent.Insert(match.Index + offset, replacement);
                
                offset += newLength - originalLength;
            }
            catch (Exception ex)
            {
                // Add error to system message
                var errorMsg = $"Error reading {match.Value}: {ex.Message}";
                systemContentParts.Add(errorMsg);
                
                // Replace with error placeholder in user content
                var replacement = GetUserContentReplacement(match);
                var originalLength = match.Value.Length;
                var newLength = replacement.Length;
                
                userContent.Remove(match.Index + offset, originalLength);
                userContent.Insert(match.Index + offset, replacement);
                
                offset += newLength - originalLength;
            }
        }

        // Clean up extra whitespace from user content
        var cleanedUserContent = userContent.ToString().Trim();
        
        var systemMessage = systemContentParts.Count > 0 
            ? string.Join("\n\n", systemContentParts)
            : string.Empty;

        return new FileReferenceResult 
        { 
            ProcessedUserContent = cleanedUserContent,
            SystemMessageContent = systemMessage,
            HasFileReferences = true
        };
    }

    private async Task<string> ProcessSingleFileReferenceAsync(Match match)
    {
        var path = match.Groups["path"].Value;
        var startLineStr = match.Groups["startLine"].Value;
        var endLineStr = match.Groups["endLine"].Value;

        // Resolve file path
        var resolvedPath = ResolvePath(path);
        if (!File.Exists(resolvedPath))
        {
            // Try to find a similar file using search
            var baseDirectory = Directory.GetCurrentDirectory();
            var searchResults = _fileSearchService.SearchFiles(path, baseDirectory, 1);
            
            if (searchResults.Count > 0)
            {
                resolvedPath = Path.Combine(baseDirectory, searchResults[0].RelativePath);
                if (!File.Exists(resolvedPath))
                {
                    throw new FileNotFoundException($"File not found: {path}. Did you mean: {searchResults[0].RelativePath}?");
                }
            }
            else
            {
                throw new FileNotFoundException($"File not found: {path}");
            }
        }

        // Read file content
        var content = await File.ReadAllTextAsync(resolvedPath);
        var lines = content.Split('\n');

        // Extract specific lines if specified
        if (!string.IsNullOrEmpty(startLineStr))
        {
            if (!int.TryParse(startLineStr, out var startLine) || startLine < 1)
            {
                throw new ArgumentException($"Invalid start line number: {startLineStr}");
            }

            var endLine = startLine;
            if (!string.IsNullOrEmpty(endLineStr))
            {
                if (!int.TryParse(endLineStr, out endLine) || endLine < startLine)
                {
                    throw new ArgumentException($"Invalid end line number: {endLineStr}");
                }
            }

            // Convert to 0-based indexing
            startLine--;
            endLine--;

            if (startLine >= lines.Length)
            {
                throw new ArgumentOutOfRangeException($"Start line {startLine + 1} exceeds file length ({lines.Length} lines)");
            }

            endLine = Math.Min(endLine, lines.Length - 1);
            var selectedLines = lines.Skip(startLine).Take(endLine - startLine + 1);
            content = string.Join('\n', selectedLines);
        }

        return content;
    }

    private string BuildSystemMessage(Match match, string fileContent)
    {
        var path = match.Groups["path"].Value;
        var startLineStr = match.Groups["startLine"].Value;
        var endLineStr = match.Groups["endLine"].Value;
        
        var fileExtension = Path.GetExtension(path).TrimStart('.');
        if (string.IsNullOrEmpty(fileExtension))
        {
            fileExtension = "text";
        }

        var lineInfo = "";
        if (!string.IsNullOrEmpty(startLineStr))
        {
            if (!string.IsNullOrEmpty(endLineStr))
            {
                lineInfo = $", lines {startLineStr}–{endLineStr}";
            }
            else
            {
                lineInfo = $", line {startLineStr}";
            }
        }

        return $"The user referenced the following code from {path}{lineInfo}:\n```{fileExtension}\n{fileContent}\n```";
    }

    private string GetUserContentReplacement(Match match)
    {
        var path = match.Groups["path"].Value;
        var startLineStr = match.Groups["startLine"].Value;
        var endLineStr = match.Groups["endLine"].Value;

        if (!string.IsNullOrEmpty(startLineStr))
        {
            if (!string.IsNullOrEmpty(endLineStr))
            {
                return $"{path}:{startLineStr}-{endLineStr}";
            }
            else
            {
                return $"{path}:{startLineStr}";
            }
        }

        return path;
    }

    private string ResolvePath(string path)
    {
        // If it's already an absolute path, return as-is
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        // Try resolving relative to current directory
        var currentDirPath = Path.Combine(Directory.GetCurrentDirectory(), path);
        if (File.Exists(currentDirPath))
        {
            return currentDirPath;
        }

        // If not found, try some common patterns
        var commonPaths = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "src", path),
            Path.Combine(Directory.GetCurrentDirectory(), "lib", path),
            Path.Combine(Directory.GetCurrentDirectory(), "app", path),
        };

        foreach (var commonPath in commonPaths)
        {
            if (File.Exists(commonPath))
            {
                return commonPath;
            }
        }

        // Return original path if nothing found (will cause FileNotFoundException)
        return path;
    }

    public bool IsValidFileReference(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return false;

        return FileReferenceRegex.IsMatch(reference);
    }
}