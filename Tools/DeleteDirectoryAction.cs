using duo_code.Tools.Core;

namespace duo_code.Tools;

public class DeleteDirectoryAction : ToolActionBase
{
    public override string ToolName => "DELETE_DIR";
    public override string Description => @"Delete a directory and all its contents.
Format:
DELETE_DIR: directory_path";
    
    public string Path { get; set; } = string.Empty;

    protected override string ExecuteCore(string baseDirectory)
    {
        var fullPath = ResolvePath(baseDirectory, Path);
        if (Directory.Exists(fullPath))
        {
            // The 'true' recursively deletes all contents.
            Directory.Delete(fullPath, true);
            return $"Successfully deleted directory and its contents: {Path}";
        }
        else
        {
            return $"Directory not found to delete: {Path}";
        }
    }

    public override string ToString()
    {
        return $"{ToolName}: {Path}";
    }
}