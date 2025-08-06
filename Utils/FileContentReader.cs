using System.Text;

namespace duo_code.Utils;

public static class FileContentReader
{
    public static (string content, int lineCount) ReadFileContent(string filePath, int startLine = 1, int endLine = -1)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return ("Error: File path cannot be empty", 0);
            
        if (!File.Exists(filePath))
            return ($"Error: File not found: {filePath}", 0);

        if (startLine < 1)
            return ("Error: Start line must be at least 1", 0);
            
        if (endLine != -1 && endLine < startLine)
            return ("Error: End line cannot be less than start line", 0);

        var result = new StringBuilder();
        var linesRead = 0;
        var currentLine = 0;
        var totalLines = 0;
        
        try
        {
            // First pass: count total lines to determine padding width
            totalLines = File.ReadLines(filePath).Count();
            var lineNumberWidth = GetLineNumberWidth(totalLines);
            
            // Second pass: read the requested lines with proper formatting
            foreach (var line in File.ReadLines(filePath))
            {
                currentLine++;
                
                if (currentLine < startLine)
                    continue;
                    
                if (endLine != -1 && currentLine > endLine)
                    break;
                    
                var lineNumber = FormatLineNumber(currentLine, lineNumberWidth);
                result.AppendLine($"{lineNumber}{line}");
                linesRead++;
            }
            
            if (linesRead == 0 && currentLine > 0)
            {
                if (startLine > currentLine)
                    return ($"Error: Start line {startLine} is beyond file length ({currentLine} lines)", 0);
            }
        }
        catch (UnauthorizedAccessException)
        {
            return ($"Error: Access denied to file: {filePath}", 0);
        }
        catch (IOException ex)
        {
            return ($"Error: IO error reading file: {ex.Message}", 0);
        }
        catch (Exception ex)
        {
            return ($"Error: Unexpected error reading file: {ex.Message}", 0);
        }
        
        return (result.ToString(), linesRead);
    }
    
    private static int GetLineNumberWidth(int totalLines)
    {
        // Determine padding width based on file size
        if (totalLines < 1000) return 3;
        if (totalLines < 10000) return 4;
        if (totalLines < 100000) return 5;
        return 6; // Support up to 999,999 lines
    }
    
    private static string FormatLineNumber(int lineNumber, int width)
    {
        // Format: [L001], [L0001], etc.
        return $"[L{lineNumber.ToString().PadLeft(width, '0')}]";
    }
}