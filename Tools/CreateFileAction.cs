using duo_code.Tools.Core;

namespace duo_code.Tools;

public class CreateFileAction : ToolActionBase
{
    public override string ToolName => "CREATE_FILE";
    public override string Description => @"Create a new file with specified content. Will override existing files.
Format:
CREATE_FILE: file_path
file_content";
    
    public string Path { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    
    protected override string ExecuteCore(string baseDirectory)
    {
        var fullPath = ResolvePath(baseDirectory, Path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, Content);
        return $"Successfully created file: {Path}";
    }
    
    public override string ToString()
    {
        return $"{ToolName}: {Path}";
    }
}