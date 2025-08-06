using duo_code.Services;

namespace duo_code.Tools.Core;

public abstract class ToolActionBase : IToolAction
{
    public abstract string ToolName { get; }
    public abstract string Description { get; }
    public virtual bool RequiresConfirmation => false; // Default to false for safe tools
    
    // Request
    public string? ConsoleRequestMessage { get; set; }

    // Tool result storage
    public string? ResultMessage { get; set; }
    public string? ConsoleResultMessage { get; set; }

    // Service for AI summarization (optional, set by factory if needed)
    public CompressCodeService? CompressService { get; set; }
    
    // Template method pattern - calls ExecuteCore and then handles summarization
    public string Execute(string baseDirectory)
    {
        ResultMessage = ExecuteCore(baseDirectory);

        // Set console message
        ConsoleResultMessage = ConsoleResultMessage ?? ResultMessage;
        
        return ResultMessage;
    }

    public void SkipExecution()
    {
        ResultMessage = "Tool execution skipped due to prior user cancellation";
    }

    public string CancelExecution()
    {
        ConsoleResultMessage = ResultMessage = "Tool execution cancelled by user";
        
        return ResultMessage;
    }
    
    // Subclasses implement the actual execution logic
    protected abstract string ExecuteCore(string baseDirectory);
        
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