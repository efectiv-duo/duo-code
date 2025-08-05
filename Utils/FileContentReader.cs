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
        
        try
        {
            foreach (var line in File.ReadLines(filePath))
            {
                currentLine++;
                
                if (currentLine < startLine)
                    continue;
                    
                if (endLine != -1 && currentLine > endLine)
                    break;
                    
                result.AppendLine($"{currentLine}:{line}");
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
}