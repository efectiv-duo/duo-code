using duo_code.Commands.Core;

namespace duo_code.Commands.Actions;

public class ExitCommand : ICommand, IHasAliases
{
    public string Name => "exit";
    public string Description => "Exit the application";
    public CommandType Type => CommandType.Action;
    public string[] GetAliases() => new[] { "q", "quit" };

    
    public Task<CommandResult> ExecuteAsync(string[] args)
    {
        return Task.FromResult(CommandResult.Exit("Goodbye!"));
    }
}