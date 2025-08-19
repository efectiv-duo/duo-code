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

    public void DisplayPreview(string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(Path))
        {
            ConsoleRequestMessage = "UPDATE_FILE: Error - File path cannot be empty";
            return;
        }
            
        if (string.IsNullOrWhiteSpace(Content))
        {
            ConsoleRequestMessage = "UPDATE_FILE: Error - Diff content cannot be empty";
            return;
        }
        
        var fullPath = ResolvePath(baseDirectory, Path);
        
        if (!File.Exists(fullPath))
        {
            ConsoleRequestMessage = $"UPDATE_FILE: Error - File not found: {Path}";
            return;
        }

        var diffLines = Content.Split('\n');
        var changeCount = diffLines.Count(line => 
            line.StartsWith("+") || line.StartsWith("-"));
        
        // Display the colored preview directly        
        foreach (var line in diffLines)
        {
            if (string.IsNullOrWhiteSpace(line)) 
                continue;

            if (line.StartsWith("@@"))
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine(line);
                Console.ResetColor();
            }
            else if (line.StartsWith("+"))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(line);
                Console.ResetColor();
            }
            else if (line.StartsWith("-"))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(line);
                Console.ResetColor();
            }
            else if (!line.StartsWith("UPDATE_FILE:"))
            {
                Console.WriteLine(line);
            }
        }
        
        // Set an empty message since we've already displayed the preview
        ConsoleRequestMessage = string.Empty;
    }

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
            
            BuildConsoleMessage(changeCount);
            
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
    
    private void BuildConsoleMessage(int changeCount)
    {
        ConsoleResultMessage = $"Successfully applied {changeCount} change{(changeCount != 1 ? "s" : "")} to {Path}";
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
}