using TextDiff;
using duo_code.Tools.Core;
using System.Text;

namespace duo_code.Tools;

public class UpdateFileAction : ToolActionBase
{
    public override string ToolName => "UPDATE_FILE";
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
        var fullPath = ResolvePath(baseDirectory, Path);
        if (!File.Exists(fullPath))
        {
            return $"Error: File not found: {Path}";
        }

        if (string.IsNullOrWhiteSpace(Content))
        {
            return "Error: Diff content cannot be empty.";
        }

        try
        {
            // Remove lines starting with \ (like "\ No newline at end of file")
            var cleanedContent = string.Join('\n', 
                Content.Split('\n')
                    .Where(line => !line.StartsWith("\\")));
            
            // 1. Read the original content from the file.
            var originalContent = File.ReadAllText(fullPath);

            // 2. Create a TextDiffer instance
            var textDiffer = new TextDiffer();

            // 3. Process the diff
            var result = textDiffer.Process(originalContent, cleanedContent);

            // 4. Write the updated content back to the file
            File.WriteAllText(fullPath, result.Text);

            // Count the number of changes (lines starting with + or -)
            var diffLines = cleanedContent.Split('\n');
            var changeCount = diffLines.Count(line => 
                line.StartsWith("+") || line.StartsWith("-"));
            
            // Build formatted console message with colored diff
            var consoleOutput = new StringBuilder();
            consoleOutput.AppendLine($"Applied {changeCount} change{(changeCount != 1 ? "s" : "")} to {Path}");

            foreach (var line in diffLines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                
                // Ensure the line is properly escaped before applying markup
                var escapedLine = EscapeMarkup(line);
                
                if (line.StartsWith("@@"))
                {
                    consoleOutput.AppendLine($"[blue]{escapedLine}[/]");
                }
                else if (line.StartsWith("+"))
                {
                    consoleOutput.AppendLine($"[green]{escapedLine}[/]");
                }
                else if (line.StartsWith("-"))
                {
                    consoleOutput.AppendLine($"[red]{escapedLine}[/]");
                }
                else if (!line.StartsWith("UPDATE_FILE:"))
                {
                    consoleOutput.AppendLine($"[dim]{escapedLine}[/]");
                }
            }
            
            ConsoleMessage = consoleOutput.ToString().TrimEnd();

            return $"Successfully applied {changeCount} change{(changeCount != 1 ? "s" : "")} to: {Path}";
        }
        catch (Exception ex)
        {
            // Catch potential errors from the library or file system.
            return $"Error applying diff: {EscapeMarkup(ex.Message)}";
        }
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