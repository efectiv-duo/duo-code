using duo_code.Commands.Core;

namespace duo_code.Commands.Actions;

public class ClearCommand : ICommand
{
    public string Name => "clear";
    public string Description => "Clear the console screen";
    public CommandType Type => CommandType.Action;
    
    public Task<CommandResult> ExecuteAsync(string[] args)
    {
        Console.Clear();
        return Task.FromResult(CommandResult.Ok("Console cleared"));
    }
}