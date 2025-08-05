using duo_code.Tools.Core;

namespace duo_code.Tools;

public class CreateDirectoryAction : ToolActionBase
{
    public override string ToolName => "CREATE_DIR";
    public override string Description => @"Create a new directory.
Format:
CREATE_DIR: directory_path";
    
    public string Path { get; set; } = string.Empty;

    protected override string ExecuteCore(string baseDirectory)
    {
        var fullPath = ResolvePath(baseDirectory, Path);
        // Directory.CreateDirectory doesn't throw if the directory already exists.
        Directory.CreateDirectory(fullPath);
        return $"Successfully created dir: {Path}";
    }

    public override string ToString()
    {
        return $"{ToolName}: {Path}";
    }
}