using duo_code.Tools.Core;

namespace duo_code.Tools;

public class RenameFileAction : ToolActionBase
{
    public override string ToolName => "RENAME_FILE";
    public override string Description => @"Rename or move a file.
Format:
RENAME_FILE: old_path > new_path";
    
    public string OldPath { get; set; } = string.Empty;
    public string NewPath { get; set; } = string.Empty;
    
    protected override string ExecuteCore(string baseDirectory)
    {
        var fullOldPath = ResolvePath(baseDirectory, OldPath);
        var fullNewPath = ResolvePath(baseDirectory, NewPath);
        if (!File.Exists(fullOldPath)) throw new FileNotFoundException($"Source file not found: {OldPath}");
        File.Move(fullOldPath, fullNewPath);
        return $"Renamed {OldPath} to {NewPath}";
    }
    
    public override string ToString()
    {
        return $"{ToolName}: {OldPath} > {NewPath}";
    }
}