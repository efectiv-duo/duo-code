using System.Text;
using duo_code.Tools.Core;

namespace duo_code.Tools;

public class UpdateFileAction : ToolActionBase
{
    public override string ToolName => "UPDATE_FILE";
    public override string Description => @"Apply unified diff format to files. Handles multiple hunks with context validation.
Format:
UPDATE_FILE: file_path
@@ -start,count +start,count @@
 context line
-removed line
+added line
 context line";
    
    public string Path { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    
    protected override string ExecuteCore(string baseDirectory)
    {
        var fullPath = ResolvePath(baseDirectory, Path);
        if (!File.Exists(fullPath))
            return $"Error: File not found: {Path}";
        
        try
        {
            var originalContent = File.ReadAllText(fullPath);
            var originalLines = originalContent.Split('\n');
            
            var hunks = ParseUnifiedDiff(Content);
            var result = ApplyHunks(originalLines, hunks);
            
            File.WriteAllText(fullPath, string.Join('\n', result));
            return $"Successfully applied {hunks.Count} hunk(s) to: {Path}";
        }
        catch (Exception ex)
        {
            return $"Error applying diff: {ex.Message}";
        }
    }
    
    private List<DiffHunk> ParseUnifiedDiff(string diffContent)
    {
        var hunks = new List<DiffHunk>();
        var lines = diffContent.Split('\n');
        
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.StartsWith("@@"))
            {
                var hunk = ParseHunk(lines, ref i);
                if (hunk != null) hunks.Add(hunk);
            }
        }
        
        return hunks;
    }
    
    private DiffHunk? ParseHunk(string[] lines, ref int index)
    {
        var headerLine = lines[index];
        
        // Parse @@ -oldStart,oldCount +newStart,newCount @@
        var parts = headerLine.Split(' ');
        if (parts.Length < 3) return null;
        
        var oldPart = parts[1].TrimStart('-').Split(',');
        var newPart = parts[2].TrimStart('+').Split(',');
        
        if (!int.TryParse(oldPart[0], out int oldStart)) return null;
        if (!int.TryParse(newPart[0], out int newStart)) return null;
        
        var hunk = new DiffHunk
        {
            OldStart = oldStart - 1, // Convert to 0-based
            NewStart = newStart - 1,
            Lines = new List<DiffLine>()
        };
        
        index++; // Move past header
        
        // Parse hunk content
        while (index < lines.Length && !lines[index].StartsWith("@@"))
        {
            var line = lines[index];
            if (string.IsNullOrEmpty(line) && index == lines.Length - 1) break;
            
            var type = line.Length > 0 ? line[0] : ' ';
            var content = line.Length > 1 ? line.Substring(1) : "";
            
            hunk.Lines.Add(new DiffLine
            {
                Type = type,
                Content = content
            });
            
            index++;
        }
        index--; // Back up for outer loop increment
        
        return hunk;
    }
    
    private string[] ApplyHunks(string[] originalLines, List<DiffHunk> hunks)
    {
        var result = originalLines.ToList();
        int offset = 0; // Track line number changes from previous hunks
        
        foreach (var hunk in hunks)
        {
            var adjustedStart = hunk.OldStart + offset;
            var applied = ApplyHunk(result, hunk, adjustedStart);
            if (!applied.Success)
            {
                throw new InvalidOperationException($"Hunk failed: {applied.Error}");
            }
            offset += applied.LineOffset;
        }
        
        return result.ToArray();
    }
    
    private (bool Success, int LineOffset, string Error) ApplyHunk(List<string> lines, DiffHunk hunk, int startLine)
    {
        // Validate context before applying
        var validation = ValidateHunkContext(lines, hunk, startLine);
        if (!validation.Success)
        {
            return (false, 0, validation.Error);
        }
        
        var modifications = new List<(int index, string action, string content)>();
        var currentLine = startLine;
        
        // Plan all modifications
        foreach (var diffLine in hunk.Lines)
        {
            switch (diffLine.Type)
            {
                case ' ': // Context line - just advance
                    currentLine++;
                    break;
                case '-': // Remove line
                    modifications.Add((currentLine, "remove", diffLine.Content));
                    currentLine++;
                    break;
                case '+': // Add line
                    modifications.Add((currentLine, "add", diffLine.Content));
                    break;
            }
        }
        
        // Apply modifications in reverse order to maintain line numbers
        var lineOffset = 0;
        for (int i = modifications.Count - 1; i >= 0; i--)
        {
            var (index, action, content) = modifications[i];
            
            if (action == "remove")
            {
                lines.RemoveAt(index);
                lineOffset--;
            }
            else if (action == "add")
            {
                lines.Insert(index, content);
                lineOffset++;
            }
        }
        
        return (true, lineOffset, "");
    }
    
    private (bool Success, string Error) ValidateHunkContext(List<string> lines, DiffHunk hunk, int startLine)
    {
        var currentLine = startLine;
        
        foreach (var diffLine in hunk.Lines)
        {
            if (diffLine.Type == ' ') // Context line
            {
                if (currentLine >= lines.Count)
                {
                    return (false, $"Context validation failed: line {currentLine + 1} beyond file end");
                }
                
                if (lines[currentLine].TrimEnd('\r') != diffLine.Content.TrimEnd('\r'))
                {
                    return (false, $"Context mismatch at line {currentLine + 1}: expected '{diffLine.Content}', got '{lines[currentLine]}'");
                }
            }
            else if (diffLine.Type == '-') // Line to remove
            {
                if (currentLine >= lines.Count)
                {
                    return (false, $"Remove validation failed: line {currentLine + 1} beyond file end");
                }
                
                if (lines[currentLine].TrimEnd('\r') != diffLine.Content.TrimEnd('\r'))
                {
                    return (false, $"Remove mismatch at line {currentLine + 1}: expected '{diffLine.Content}', got '{lines[currentLine]}'");
                }
            }
            
            if (diffLine.Type != '+') currentLine++;
        }
        
        return (true, "");
    }
    
    public override string ToString()
    {
        return $"{ToolName}: {Path}";
    }
}

internal class DiffHunk
{
    public int OldStart { get; set; }
    public int NewStart { get; set; }
    public List<DiffLine> Lines { get; set; } = new();
}

internal class DiffLine
{
    public char Type { get; set; } // ' ', '-', '+'
    public string Content { get; set; } = "";
}