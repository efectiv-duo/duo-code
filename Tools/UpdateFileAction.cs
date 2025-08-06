using TextDiff;
using duo_code.Tools.Core;
using duo_code.Utils;
using System.Text;

namespace duo_code.Tools;

public class UpdateFileAction : ToolActionBase
{
    public override string ToolName => "UPDATE_FILE";
       public override bool RequiresConfirmation => true;
    public override string Description => @"Applies Unified Diff format changes to a file.
Format:
UPDATE_FILE: file_path
@@ -1,3 +1,4 @@
Line 1
+Line 1.5
Line 2
-Line 3
+Line Three";

    public string Path { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty; // This will hold the diff text

    protected override string ExecuteCore(string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(Path))
            return "Error: File path cannot be empty";
            
        if (string.IsNullOrWhiteSpace(Content))
            return "Error: Diff content cannot be empty";
            
        var fullPath = ResolvePath(baseDirectory, Path);
        
        if (!File.Exists(fullPath))
            return $"Error: File not found: {Path}";

        try
        {
            var cleanedContent = string.Join('\n', 
                Content.Split('\n')
                    .Where(line => !line.StartsWith("\\")));
            
            var originalContent = File.ReadAllText(fullPath);
            var textDiffer = new TextDiffer();
            var result = textDiffer.Process(originalContent, cleanedContent);
            
            File.WriteAllText(fullPath, result.Text);

            var diffLines = cleanedContent.Split('\n');
            var changeCount = diffLines.Count(line => 
                line.StartsWith("+") || line.StartsWith("-"));
            
            BuildConsoleMessage(diffLines, changeCount);
            
            return BuildResponse(fullPath, changeCount);
        }
        catch (UnauthorizedAccessException)
        {
            return $"Error: Access denied to file: {Path}";
        }
        catch (IOException ex)
        {
            return $"Error: IO error applying diff: {ex.Message}";
        }
        catch (Exception ex)
        {
            return $"Error applying diff: {ex.Message}";
        }
    }
    
    private void BuildConsoleMessage(string[] diffLines, int changeCount)
    {
        var consoleOutput = new StringBuilder();
        consoleOutput.AppendLine($"Applied {changeCount} change{(changeCount != 1 ? "s" : "")} to {Path}");

        foreach (var line in diffLines)
        {
            if (string.IsNullOrWhiteSpace(line)) 
                continue;
                
            var escapedLine = EscapeMarkup(line);
            
            if (line.StartsWith("@@"))
                consoleOutput.AppendLine($"[blue]{escapedLine}[/]");
            else if (line.StartsWith("+"))
                consoleOutput.AppendLine($"[green]{escapedLine}[/]");
            else if (line.StartsWith("-"))
                consoleOutput.AppendLine($"[red]{escapedLine}[/]");
            else if (!line.StartsWith("UPDATE_FILE:"))
                consoleOutput.AppendLine($"[dim]{escapedLine}[/]");
        }
        
        ConsoleResultMessage = consoleOutput.ToString().TrimEnd();
    }
    
    private string BuildResponse(string fullPath, int changeCount)
    {
        var response = new StringBuilder();
        response.AppendLine($"Successfully applied {changeCount} change{(changeCount != 1 ? "s" : "")} to: {Path}");
        response.AppendLine();
        
        var (content, lineCount) = FileContentReader.ReadFileContent(fullPath);
        response.AppendLine($"Full file result ({lineCount} lines):");
        response.Append(content);

        return response.ToString();
    }

    public override string ToString()
    {
        return $"{ToolName}: {Path}";
    }
    
    private static string EscapeMarkup(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;
            
        // Escape special markup characters
        return text
            .Replace("[", "[[")
            .Replace("]", "]]");
    }
}