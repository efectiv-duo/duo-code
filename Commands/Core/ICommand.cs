namespace duo_code.Commands.Core;

public interface ICommand
{
    string Name { get; }
    string Description { get; }
    CommandType Type { get; }
    
    Task<CommandResult> ExecuteAsync(string[] args);
}

public enum CommandType
{
    Action,  // Direct action commands executed by CLI
    Prompt   // Commands that submit a prompt to the AI
}

public class CommandResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? PromptToSubmit { get; set; } // For prompt commands
    public bool ShouldExit { get; set; } // For exit command
    
    public static CommandResult Ok(string message) => new() { Success = true, Message = message };
    public static CommandResult Error(string message) => new() { Success = false, Message = message };
    public static CommandResult Prompt(string prompt) => new() { Success = true, PromptToSubmit = prompt };
    public static CommandResult Exit(string message) => new() { Success = true, Message = message, ShouldExit = true };
}