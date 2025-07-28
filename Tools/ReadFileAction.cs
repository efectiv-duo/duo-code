using duo_code.Tools.Core;
using static duo_code.Models.ChatCompletionChunk;

namespace duo_code.Tools;

public class ReadFileAction : ToolActionBase
{
    public override string ToolName => "READ_FILE";
    public override string Description => @"Read the content of a file with optional line range.
Format:
READ_FILE: file_path
READ_FILE: file_path lines:start-end
READ_FILE: file_path lines:start+count";
    
    public string Path { get; set; } = string.Empty;
    public string? LineRange { get; set; }
    
    // File size limit: 10 MB
    private const long MaxFileSize = 10 * 1024 * 1024;
    
    // Known binary file extensions
    private static readonly string[] BinaryExtensions = {
        ".exe", ".dll", ".so", ".a", ".o", ".zip", ".rar", ".7z", ".tar", ".gz",
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".pdf", ".doc", ".docx", ".xls", ".xlsx",
        ".ppt", ".pptx", ".iso", ".img", ".bin", ".mp3", ".mp4", ".avi", ".mov", ".wav"
    };
    
    protected override string ExecuteCore(string baseDirectory)
    {
        var fullPath = ResolvePath(baseDirectory, Path);
        if (!File.Exists(fullPath)) 
            return $"Error: File not found: {Path}";
        
        // Check for binary file extension
        var extension = System.IO.Path.GetExtension(fullPath).ToLowerInvariant();
        if (BinaryExtensions.Contains(extension))
        {
            return $"Error: Cannot read binary file type ('{extension}'). The READ_FILE tool is for text-based files.";
        }
        
        // Check file size
        var fileInfo = new FileInfo(fullPath);
        if (fileInfo.Length > MaxFileSize)
        {
            return $"Error: File '{Path}' is too large ({fileInfo.Length / 1024 / 1024} MB). Maximum allowed size is {MaxFileSize / 1024 / 1024} MB.";
        }
        
        try
        {
            // Parse line range if provided
            if (!string.IsNullOrEmpty(LineRange))
            {
                return ReadFileWithLineRange(fullPath, LineRange);
            }
            
            // Read entire file with line numbers
            var lines = File.ReadAllLines(fullPath);
            var result = new System.Text.StringBuilder();
            result.AppendLine($"Executed READ_FILE: {Path}");
            
            for (int i = 0; i < lines.Length; i++)
            {
                result.AppendLine($"{i + 1}: {lines[i]}");
            }
            
            return result.ToString();
        }
        catch (UnauthorizedAccessException)
        {
            return $"Error: Access denied to file: {Path}";
        }
        catch (IOException ex)
        {
            return $"Error reading file: {ex.Message}";
        }
    }
    
    protected override string? CreateSummary(string fullResult)
    {
        // For small files, no summary needed
        if (fullResult.Length <= 500) return null;
        
        // Trigger async AI summarization if service is available
        if (CompressService != null)
        {
            _ = Task.Run(async () => 
            {
                try
                {
                    var summary = await CompressService.CompressCodeAsync(fullResult);
                    SummarizedResult = $"[File content summary]\n{summary}";
                }
                catch { /* Ignore summarization errors */ }
            });
            
            // Return a placeholder while summarization happens
            return "[Truncated file content]\n" + GetTruncatedContent(fullResult);
        }
        
        // Fallback to simple truncation
        return GetTruncatedContent(fullResult);
    }
    
    private string ReadFileWithLineRange(string filePath, string lineRange)
    {
        try
        {
            var lines = File.ReadAllLines(filePath);
            var (startLine, endLine) = ParseLineRange(lineRange, lines.Length);
            
            if (startLine < 1 || startLine > lines.Length)
            {
                return $"Error: Start line {startLine} is out of range (1-{lines.Length})";
            }
            
            endLine = Math.Min(endLine, lines.Length);
            var selectedLines = lines.Skip(startLine - 1).Take(endLine - startLine + 1);
            
            var result = new System.Text.StringBuilder();
            result.AppendLine($"Executed READ_FILE: {Path} lines:{startLine}-{endLine}");
            
            int currentLine = startLine;
            foreach (var line in selectedLines)
            {
                result.AppendLine($"{currentLine,4}: {line}");
                currentLine++;
            }
            
            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"Error reading file with line range: {ex.Message}";
        }
    }
    
    private (int startLine, int endLine) ParseLineRange(string lineRange, int totalLines)
    {
        if (lineRange.Contains('-'))
        {
            // Format: start-end
            var parts = lineRange.Split('-');
            if (parts.Length == 2 && int.TryParse(parts[0], out int start) && int.TryParse(parts[1], out int end))
            {
                return (start, end);
            }
        }
        else if (lineRange.Contains('+'))
        {
            // Format: start+count
            var parts = lineRange.Split('+');
            if (parts.Length == 2 && int.TryParse(parts[0], out int start) && int.TryParse(parts[1], out int count))
            {
                return (start, start + count - 1);
            }
        }
        else if (int.TryParse(lineRange, out int singleLine))
        {
            // Single line number
            return (singleLine, singleLine);
        }
        
        // Default to entire file if parsing fails
        return (1, totalLines);
    }
    
    private string GetTruncatedContent(string content)
    {
        var lines = content.Split('\n');
        if (lines.Length > 50)
        {
            var truncated = string.Join('\n', lines.Take(50));
            return $"{truncated}\n... + {lines.Length - 50} more lines";
        }
        return content;
    }
    
    public override string ToString()
    {
        return $"{ToolName}: {Path}";
    }
}