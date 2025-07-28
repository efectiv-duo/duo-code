using duo_code.Tools.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace duo_code.Tools
{
    public class EditFileAction : ToolActionBase
    {
        public override string ToolName => "EDIT_FILE";
        public override string Description => @"Edit a file by replacing, deleting, or adding lines of content. Simpler and more direct than UPDATE_FILE.
Format:
EDIT_FILE: file_path [instruction]
content_for_edit

Instructions:
- lines:START-END   (Replace lines from START to END. Line numbers are inclusive.)
- delete:START-END  (Delete lines from START to END.)
- before:LINE       (Insert content before line number LINE.)
- after:LINE        (Insert content after line number LINE.)
- append            (Add content to the end of the file.)
- prepend           (Add content to the start of the file.)

Examples:
1. Replace lines 10 to 15:
EDIT_FILE: src/app.js lines:10-15
// New replacement content
// goes on these lines.

2. Delete lines 25 to 30:
EDIT_FILE: config.json delete:25-30

3. Insert new content before line 42:
EDIT_FILE: styles.css before:42
.new-class { color: red; }
";

        public string Path { get; set; } = string.Empty;
        public string Instruction { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;

        protected override string ExecuteCore(string baseDirectory)
        {
            var fullPath = ResolvePath(baseDirectory, Path);
            if (!File.Exists(fullPath))
            {
                return $"Error: File not found: {Path}";
            }

            var lines = new List<string>(File.ReadAllLines(fullPath));
            var newContentLines = Content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            try
            {
                var parts = Instruction.Split(new[] { ':', '-' }, StringSplitOptions.RemoveEmptyEntries);
                var action = parts[0].ToLowerInvariant();

                switch (action)
                {
                    case "lines": // Replace
                        if (parts.Length != 3 || !int.TryParse(parts[1], out var start) || !int.TryParse(parts[2], out var end))
                            return "Error: Invalid format for 'lines'. Use 'lines:START-END'.";

                        ApplyReplace(lines, start, end, newContentLines);
                        break;

                    case "delete":
                        if (parts.Length != 3 || !int.TryParse(parts[1], out var delStart) || !int.TryParse(parts[2], out var delEnd))
                            return "Error: Invalid format for 'delete'. Use 'delete:START-END'.";

                        ApplyReplace(lines, delStart, delEnd, new string[0]); // Replace with nothing
                        break;

                    case "before":
                        if (parts.Length != 2 || !int.TryParse(parts[1], out var beforeLine))
                            return "Error: Invalid format for 'before'. Use 'before:LINE'.";

                        ApplyInsert(lines, beforeLine - 1, newContentLines);
                        break;

                    case "after":
                        if (parts.Length != 2 || !int.TryParse(parts[1], out var afterLine))
                            return "Error: Invalid format for 'after'. Use 'after:LINE'.";

                        ApplyInsert(lines, afterLine, newContentLines);
                        break;

                    case "append":
                        lines.AddRange(newContentLines);
                        break;

                    case "prepend":
                        lines.InsertRange(0, newContentLines);
                        break;

                    default:
                        return $"Error: Unknown instruction '{action}'. Valid instructions are: lines, delete, before, after, append, prepend.";
                }

                // Use a StringBuilder for efficient file content creation
                var resultBuilder = new StringBuilder();
                for (int i = 0; i < lines.Count; i++)
                {
                    resultBuilder.Append(lines[i]);
                    if (i < lines.Count - 1) // Avoid adding a trailing newline
                    {
                        resultBuilder.Append(Environment.NewLine);
                    }
                }

                File.WriteAllText(fullPath, resultBuilder.ToString());
                return $"Successfully applied edit to: {Path}";
            }
            catch (Exception ex)
            {
                return $"Error processing edit command: {ex.Message}";
            }
        }

        private void ApplyReplace(List<string> lines, int startLine, int endLine, string[] newContent)
        {
            if (startLine <= 0 || endLine > lines.Count || startLine > endLine)
                throw new ArgumentOutOfRangeException(nameof(startLine), $"Line numbers [{startLine}-{endLine}] are out of the file's bounds (1-{lines.Count}).");

            int startIndex = startLine - 1;
            int count = endLine - startLine + 1;

            lines.RemoveRange(startIndex, count);
            lines.InsertRange(startIndex, newContent);
        }

        private void ApplyInsert(List<string> lines, int index, string[] newContent)
        {
            if (index < 0 || index > lines.Count)
                throw new ArgumentOutOfRangeException(nameof(index), $"Insertion line number is out of the file's bounds (0-{lines.Count}).");

            lines.InsertRange(index, newContent);
        }

        public override string ToString()
        {
            return $"{ToolName}: {Path} {Instruction}";
        }
    }
}