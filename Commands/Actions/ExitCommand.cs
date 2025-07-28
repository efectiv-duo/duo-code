using duo_code.Commands.Core;

namespace duo_code.Commands.Actions;

public class ExitCommand : ICommand
{
    public string Name => "exit";
    public string Description => "Exit the application";
    public CommandType Type => CommandType.Action;
    
    public Task<CommandResult> ExecuteAsync(string[] args)
    {
        return Task.FromResult(CommandResult.Exit("Goodbye!"));
    }
}