using duo_code.Tools.Core;

namespace duo_code.Tools;

public class DeleteFileAction : ToolActionBase
{
    public override string ToolName => "DELETE_FILE";
    public override string Description => @"Delete a file.
Format:
DELETE_FILE: file_path";
    
    public string Path { get; set; } = string.Empty;

    protected override string ExecuteCore(string baseDirectory)
    {
        var fullPath = ResolvePath(baseDirectory, Path);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            return $"Successfully deleted file: {Path}";
        }
        else
        {
            // It's useful to tell the AI if its assumption was wrong
            return $"File not found to delete: {Path}";
        }
    }

    public override string ToString()
    {
        return $"{ToolName}: {Path}";
    }
}