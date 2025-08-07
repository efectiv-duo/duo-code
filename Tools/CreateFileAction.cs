using duo_code.Tools.Core;

namespace duo_code.Tools;

public class CreateFileAction : ToolActionBase
{
    public override string ToolName => "CREATE_FILE";
    public override bool RequiresConfirmation => true;
    public override string Description => @"Create a new file with specified content. Will override existing files.
Format:
CREATE_FILE: file_path
file_content";
    
    public string Path { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    
    protected override string ExecuteCore(string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(Path))
        {
        return "Error: File path cannot be empty or null.";
        }

       try
       {
           var fullPath = ResolvePath(baseDirectory, Path);

           var directoryName = System.IO.Path.GetDirectoryName(fullPath);
           if (!string.IsNullOrEmpty(directoryName))
           {
               Directory.CreateDirectory(directoryName);
           }

           File.WriteAllText(fullPath, Content);
           return $"Successfully created file: {Path}";
       }
       catch (UnauthorizedAccessException ex)
       {
           return $"Error: Access denied to create file at '{Path}'. Details: {ex.Message}";
       }
       catch (System.IO.PathTooLongException ex)
       {
           return $"Error: The specified path '{Path}' is too long. Details: {ex.Message}";
       }
       catch (Exception ex)
       {
           return $"Error: An unexpected error occurred while creating file '{Path}'. Details: {ex.Message}";
       }
    }
    
    public override string ToString()
    {
        return $"{ToolName}: {Path}";
    }
}