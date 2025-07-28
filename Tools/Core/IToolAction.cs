namespace duo_code.Tools.Core;

public interface IToolAction
{
    string ToolName { get; }
    string Description { get; }
    
    // Tool result storage
    string? FullResult { get; set; }
    string? SummarizedResult { get; set; }
    
    // Execute now returns a string result to be shown to the AI
    string Execute(string baseDirectory);
    string ToString();
}