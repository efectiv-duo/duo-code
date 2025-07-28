using duo_code.Tools.Core;

namespace duo_code.Tools;

public class UpdateFileAction : ToolActionBase
{
    public override string ToolName => "UPDATE_FILE";
    public override string Description => @"Overwrite an existing file with new content.
Format:
UPDATE_FILE: file_path
file_content";
    
    public string Path { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    
    protected override string ExecuteCore(string baseDirectory)
    {
        var fullPath = ResolvePath(baseDirectory, Path);
        if (!File.Exists(fullPath)) throw new FileNotFoundException($"File not found for update: {Path}");
        File.WriteAllText(fullPath, Content);
        return $"Successfully updated file: {Path}";
    }
    
    public override string ToString()
    {
        return $"{ToolName}: {Path}";
    }
}