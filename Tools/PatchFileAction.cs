using System.Text;
using System.Text.RegularExpressions;
using duo_code.Tools.Core;

namespace duo_code.Tools;

public class PatchFileAction : ToolActionBase
{
    public override string ToolName => "PATCH_FILE";
    public override string Description => @"Apply a targeted change to an existing file using a diff-like format. MUST be used for all modifications instead of UPDATE_FILE.
    Format:
    PATCH_FILE: file_path
    - content to remove
    + content to add";
    
    public string Path { get; set; } = string.Empty;
    public List<PatchOperation> Patches { get; set; } = new();
    
    protected override string ExecuteCore(string baseDirectory)
    {
        var fullPath = ResolvePath(baseDirectory, Path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"File not found: {Path}");
        }
        
        // Read current content
        var content = File.ReadAllText(fullPath);
        var originalContent = content;
        
        // Apply patches
        var appliedPatches = 0;
        foreach (var patch in Patches)
        {
            if (patch.Operation == PatchOperationType.Remove)
            {
                if (content.Contains(patch.Content))
                {
                    content = content.Replace(patch.Content, "");
                    appliedPatches++;
                }
                else
                {
                    throw new InvalidOperationException($"Content to remove not found: '{patch.Content.Substring(0, Math.Min(50, patch.Content.Length))}...'");
                }
            }
            else if (patch.Operation == PatchOperationType.Add)
            {
                // For add operations, we need to know where to add
                // If previous operation was remove, add at that position
                // Otherwise, this is an error
                if (Patches.IndexOf(patch) > 0 && Patches[Patches.IndexOf(patch) - 1].Operation == PatchOperationType.Remove)
                {
                    // This is handled by the replace logic below
                    continue;
                }
                else
                {
                    throw new InvalidOperationException("Add operation must follow a remove operation to indicate placement");
                }
            }
        }
        
        // Handle remove/add pairs as replacements
        for (int i = 0; i < Patches.Count - 1; i++)
        {
            if (Patches[i].Operation == PatchOperationType.Remove && 
                Patches[i + 1].Operation == PatchOperationType.Add)
            {
                var removeContent = Patches[i].Content;
                var addContent = Patches[i + 1].Content;
                
                if (originalContent.Contains(removeContent))
                {
                    content = originalContent.Replace(removeContent, addContent);
                    appliedPatches = 2; // Count both operations
                }
                else
                {
                    throw new InvalidOperationException($"Content to replace not found: '{removeContent.Substring(0, Math.Min(50, removeContent.Length))}...'");
                }
                
                i++; // Skip the add operation since we handled it
            }
        }
        
        // Write the modified content back
        File.WriteAllText(fullPath, content);
        
        // Store full result for display
        FullResult = $"Patched {Path} - {appliedPatches} changes applied";
        
        return $"Successfully patched file: {Path}";
    }
    
    public override string ToString()
    {
        return $"{ToolName}: {Path}";
    }
}

public class PatchOperation
{
    public PatchOperationType Operation { get; set; }
    public string Content { get; set; } = string.Empty;
}

public enum PatchOperationType
{
    Remove,
    Add
}