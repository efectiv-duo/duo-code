using duo_code.Tools.Core;

namespace duo_code.Tools;

public class ReadFileAction : ToolActionBase
{
    public override string ToolName => "READ_FILE";
    public override string Description => @"Read the entire content of a file.
Format:
READ_FILE: file_path";
    
    public string Path { get; set; } = string.Empty;
    
    protected override string ExecuteCore(string baseDirectory)
    {
        var fullPath = ResolvePath(baseDirectory, Path);
        if (!File.Exists(fullPath)) throw new FileNotFoundException($"File not found: {Path}");
        return File.ReadAllText(fullPath);
    }
    
    protected override string? CreateSummary(string fullResult)
    {
        // For small files, no summary needed
        if (fullResult.Length <= 500) return null;
        
        // Trigger async AI summarization if service is available
        if (CompressService != null)
        {
            _ = Task.Run(async () => 
            {
                try
                {
                    var summary = await CompressService.CompressCodeAsync(fullResult);
                    SummarizedResult = $"[File content summary]\n{summary}";
                }
                catch { /* Ignore summarization errors */ }
            });
            
            // Return a placeholder while summarization happens
            return "[Truncated file content]\n" + GetTruncatedContent(fullResult);
        }
        
        // Fallback to simple truncation
        return GetTruncatedContent(fullResult);
    }
    
    private string GetTruncatedContent(string content)
    {
        var lines = content.Split('\n');
        if (lines.Length > 50)
        {
            var truncated = string.Join('\n', lines.Take(50));
            return $"{truncated}\n... + {lines.Length - 50} more lines";
        }
        return content;
    }
    
    public override string ToString()
    {
        return $"{ToolName}: {Path}";
    }
}