using duo_code.Tools.Core;
using duo_code.Utils;

namespace duo_code.Tools;

public class ReadFileAction : ToolActionBase
{
    public override string ToolName => "READ_FILE";
    public override string Description => @"Read the content of a file with optional line range. Purpose must always be provided to explain why you're reading the file.
Format:
READ_FILE: file_path purpose:read_purpose
READ_FILE: file_path lines:start-end purpose:read_purpose
READ_FILE: file_path lines:start+count purpose:read_purpose";

    public string Path { get; set; } = string.Empty;
    public string? LineRange { get; set; }
    public string? Purpose { get; set; }

    // File size limit: 10 MB
    private const long MaxFileSize = 10 * 1024 * 1024;

    // Known binary file extensions
    private static readonly HashSet<string> BinaryExtensions = new()
    {
        ".exe", ".dll", ".so", ".a", ".o", ".zip", ".rar", ".7z", ".tar", ".gz",
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".pdf", ".doc", ".docx", ".xls", ".xlsx",
        ".ppt", ".pptx", ".iso", ".img", ".bin", ".mp3", ".mp4", ".avi", ".mov", ".wav"
    };

    protected override string ExecuteCore(string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(Path))
            return "Error: File path cannot be empty";
            
        var fullPath = ResolvePath(baseDirectory, Path);
        
        if (!File.Exists(fullPath))
            return $"Error: File not found: {Path}";

        var extension = System.IO.Path.GetExtension(fullPath).ToLowerInvariant();
        if (BinaryExtensions.Contains(extension))
            return $"Error: Cannot read binary file type ('{extension}'). The READ_FILE tool is for text-based files.";

        var fileInfo = new FileInfo(fullPath);
        if (fileInfo.Length > MaxFileSize)
            return $"Error: File '{Path}' is too large ({fileInfo.Length / 1024 / 1024} MB). Maximum allowed size is {MaxFileSize / 1024 / 1024} MB.";

        var result = new System.Text.StringBuilder();
        result.AppendLine($"Executed READ_FILE: {Path} {(string.IsNullOrEmpty(Purpose) ? "" : $" (Purpose: {Purpose})")}");

        var (startLine, endLine) = string.IsNullOrEmpty(LineRange) 
            ? (1, -1) 
            : ParseLineRange(LineRange);
            
        var (content, lineCount) = FileContentReader.ReadFileContent(fullPath, startLine, endLine);
        
        if (content.StartsWith("Error:"))
            return content;
            
        result.Append(content);
        ConsoleResultMessage = $"Read {lineCount} lines from {Path}";
        
        return result.ToString();
    }

    private (int startLine, int endLine) ParseLineRange(string lineRange)
    {
        if (string.IsNullOrWhiteSpace(lineRange))
            return (1, -1);
            
        if (lineRange.Contains('-'))
        {
            var parts = lineRange.Split('-', 2);
            if (parts.Length == 2 && 
                int.TryParse(parts[0].Trim(), out int start) && 
                int.TryParse(parts[1].Trim(), out int end))
            {
                return (start, end);
            }
        }
        else if (lineRange.Contains('+'))
        {
            var parts = lineRange.Split('+', 2);
            if (parts.Length == 2 && 
                int.TryParse(parts[0].Trim(), out int start) && 
                int.TryParse(parts[1].Trim(), out int count) && 
                count > 0)
            {
                return (start, start + count - 1);
            }
        }
        else if (int.TryParse(lineRange.Trim(), out int singleLine))
        {
            return (singleLine, singleLine);
        }

        return (1, -1);
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
        return $"{ToolName}: {Path}{(string.IsNullOrEmpty(Purpose) ? "" : $" (Purpose: {Purpose})")}";
    }
}