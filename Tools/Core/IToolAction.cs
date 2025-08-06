namespace duo_code.Tools.Core;

public interface IToolAction
{
    string ToolName { get; }
    string Description { get; }
    bool RequiresConfirmation { get; }

    // Tool request
    string? ConsoleRequestMessage { get; set; }

    // Tool result storage
    string? ResultMessage { get; set; }
    string? ConsoleResultMessage { get; set; }

    // Execute now returns a string result to be shown to the AI
    string Execute(string baseDirectory);
    void SkipExecution();
    string CancelExecution();
    string ToString();
}