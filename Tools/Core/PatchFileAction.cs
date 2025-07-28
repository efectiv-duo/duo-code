using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using duo_code.Models;

namespace duo_code.Tools.Core
{
    public class PatchFileAction : ToolActionBase
    {
        public override string ToolName => "PATCH_FILE";
        public override string Description => @"Apply precise changes to a file using a structured format.
    Format:
    PATCH_FILE: file_path
    - line_number: content_to_remove (exact match)
    + content_to_add (optional - for replacements/insertions)
    
    For removals: Only specify the '-' line
    For additions: Only specify the '+' line with target line number
    For replacements: Use both '-' and '+' lines together";

        public string Path { get; set; } = string.Empty;
        public List<PatchOperation> Patches { get; set; } = new();

        protected override string ExecuteCore(string baseDirectory)
        {
            var fullPath = ResolvePath(baseDirectory, Path);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"File not found: {Path}");
            }

            // Read all lines for atomic modification
            var originalLines = File.ReadAllLines(fullPath).ToList();
            var modifiedLines = new List<string>(originalLines);

            // Validate all operations first
            ValidateAllOperations(originalLines, modifiedLines);

            // Apply all modifications
            ApplyAllOperations(originalLines, modifiedLines);

            // Write back only if changes were made
            if (!originalLines.SequenceEqual(modifiedLines))
            {
                File.WriteAllLines(fullPath, modifiedLines);
                FullResult = $"Patched {Path} - {Patches.Count} changes applied";
                return $"Successfully patched file: {Path}";
            }

            FullResult = $"No changes needed for {Path}";
            return $"No changes applied to file: {Path}";
        }

        private void ValidateAllOperations(List<string> originalLines, List<string> modifiedLines)
        {
            foreach (var patch in Patches)
            {
                // Validate line numbers
                if (patch.LineNumber.HasValue)
                {
                    if (patch.LineNumber.Value < 1 || patch.LineNumber.Value > originalLines.Count)
                    {
                        throw new InvalidOperationException($"Invalid line number: {patch.LineNumber} in {Path}. File has {originalLines.Count} lines.");
                    }
                }

                // Validate content existence for removals
                if (patch.Operation == PatchOperationType.Remove)
                {
                    var targetLine = patch.LineNumber.HasValue ? 
                        originalLines[patch.LineNumber.Value - 1] : 
                        originalLines.FirstOrDefault(l => l.Contains(patch.Content, StringComparison.Ordinal));

                    if (targetLine == null)
                    {
                        throw new InvalidOperationException($"Content to remove not found: '{patch.Content}' in {Path}");
                    }

                    if (!patch.LineNumber.HasValue && originalLines.Count(l => l.Contains(patch.Content, StringComparison.Ordinal)) > 1)
                    {
                        throw new InvalidOperationException($"Ambiguous content to remove: '{patch.Content}' appears multiple times in {Path}");
                    }
                }
            }
        }

        private void ApplyAllOperations(List<string> originalLines, List<string> modifiedLines)
        {
            // Sort patches by line number (descending to avoid index shifting)
            var sortedPatches = Patches.OrderByDescending(p => p.LineNumber).ToList();

            foreach (var patch in sortedPatches)
            {
                if (patch.Operation == PatchOperationType.Remove)
                {
                    var lineIndex = patch.LineNumber.HasValue ? 
                        patch.LineNumber.Value - 1 : 
                        modifiedLines.FindIndex(l => l.Contains(patch.Content, StringComparison.Ordinal));

                    if (lineIndex >= 0)
                    {
                        modifiedLines.RemoveAt(lineIndex);
                    }
                }
                else if (patch.Operation == PatchOperationType.Add)
                {
                    var insertIndex = patch.LineNumber.HasValue ? 
                        patch.LineNumber.Value : 
                        modifiedLines.Count;

                    modifiedLines.Insert(insertIndex, patch.Content);
                }
                else if (patch.Operation == PatchOperationType.Replace)
                {
                    var lineIndex = patch.LineNumber.HasValue ? 
                        patch.LineNumber.Value - 1 : 
                        modifiedLines.FindIndex(l => l.Contains(patch.OldContent, StringComparison.Ordinal));

                    if (lineIndex >= 0)
                    {
                        modifiedLines[lineIndex] = patch.Content;
                    }
                }
            }
        }

        public override string ToString()
        {
            return $"{ToolName}: {Path} ({Patches.Count} operations)";
        }
    }

    public class PatchOperation
    {
        public PatchOperationType Operation { get; set; }
        public int? LineNumber { get; set; }
        public string Content { get; set; } = string.Empty;
        public string OldContent { get; set; } = string.Empty; // For replace operations
    }

    public enum PatchOperationType
    {
        Remove,
        Add,
        Replace
    }
}