using duo_code.Services;

namespace duo_code.Tools.Core;

public abstract class ToolActionBase : IToolAction
{
    public abstract string ToolName { get; }
    public abstract string Description { get; }
    
    // Tool result storage
    public string? FullResult { get; set; }
    public string? SummarizedResult { get; set; }
    
    // Service for AI summarization (optional, set by factory if needed)
    public CompressCodeService? CompressService { get; set; }
    
    // Template method pattern - calls ExecuteCore and then handles summarization
    public string Execute(string baseDirectory)
    {
        var result = ExecuteCore(baseDirectory);
        FullResult = result;
        
        // Let each tool decide how to summarize
        SummarizedResult = CreateSummary(result);
        
        // Always return the full result for immediate display
        return result;
    }
    
    // Subclasses implement the actual execution logic
    protected abstract string ExecuteCore(string baseDirectory);
    
    // Subclasses can override to provide custom summarization
    protected virtual string? CreateSummary(string fullResult)
    {
        // By default, no summarization
        return null;
    }
    
    public abstract override string ToString();
    
    /// <summary>
    /// Resolves a path that could be either relative or absolute
    /// </summary>
    protected string ResolvePath(string baseDirectory, string inputPath)
    {
        // If the input is already an absolute path, use it directly
        if (Path.IsPathRooted(inputPath))
        {
            return Path.GetFullPath(inputPath);
        }
        
        // Otherwise, combine with base directory
        return Path.GetFullPath(Path.Combine(baseDirectory, inputPath));
    }
}