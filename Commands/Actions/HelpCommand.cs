using duo_code.Commands.Core;

namespace duo_code.Commands.Actions;

public class HelpCommand : ICommand
{
    private readonly CommandRegistry _registry;
    
    public string Name => "help";
    public string Description => "Show available commands";
    public CommandType Type => CommandType.Action;
    
    public HelpCommand(CommandRegistry registry)
    {
        _registry = registry;
    }
    
    public Task<CommandResult> ExecuteAsync(string[] args)
    {
        var commands = _registry.GetAllCommands();
        var actionCommands = commands.Where(c => c.Type == CommandType.Action);
        var promptCommands = commands.Where(c => c.Type == CommandType.Prompt);
        
        var helpText = @"Available Commands:

Action Commands (executed by CLI):
";
        foreach (var cmd in actionCommands)
        {
            helpText += $"  /{cmd.Name} - {cmd.Description}\n";
        }
        
        helpText += @"
Prompt Commands (submitted to AI):
";
        foreach (var cmd in promptCommands)
        {
            helpText += $"  /{cmd.Name} - {cmd.Description}\n";
        }
        
        helpText += @"
Type 'exit' to quit the application.";
        
        return Task.FromResult(CommandResult.Ok(helpText));
    }
}