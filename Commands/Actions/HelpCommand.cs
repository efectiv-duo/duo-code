using duo_code.Commands.Core;

namespace duo_code.Commands.Actions;

public class HelpCommand : ICommand, IHasAliases
{
    private readonly CommandRegistry _registry;
    
    public string Name => "help";
    public string Description => "Show available commands";
    public CommandType Type => CommandType.Action;
    public string[] GetAliases() => new[] { "h", "?" };

    
    public HelpCommand(CommandRegistry registry)
    {
        _registry = registry;
    }
    
    public Task<CommandResult> ExecuteAsync(string[] args)
    {
        // Check if running in headless mode
        var isHeadless = Environment.GetCommandLineArgs().Contains("--headless");

        if (isHeadless)
        {
            return Task.FromResult(ShowInteractiveHelp());
        }
        else
        {
            return Task.FromResult(ShowTraditionalHelp());
        }
    }

    private CommandResult ShowInteractiveHelp()
    {
        var commands = _registry.GetAllCommands();
        var actionCommands = commands.Where(c => c.Type == CommandType.Action).ToList();
        var promptCommands = commands.Where(c => c.Type == CommandType.Prompt).ToList();

        var output = new System.Text.StringBuilder();
        output.AppendLine("📚 Command Help:");
        output.AppendLine();
        output.AppendLine("Current Model: Help Browser");
        output.AppendLine();
        output.AppendLine("Available Models:");

        // Show action commands as selectable options
        foreach (var cmd in actionCommands)
        {
            var aliases = cmd is IHasAliases aliasCmd ? string.Join(",", aliasCmd.GetAliases()) : "";
            var aliasText = !string.IsNullOrEmpty(aliases) ? $" (aliases: {aliases})" : "";
            output.AppendLine($"- Action - {cmd.Name}{aliasText}");
        }

        // Show prompt commands as selectable options  
        foreach (var cmd in promptCommands)
        {
            var aliases = cmd is IHasAliases aliasCmd ? string.Join(",", aliasCmd.GetAliases()) : "";
            var aliasText = !string.IsNullOrEmpty(aliases) ? $" (aliases: {aliases})" : "";
            output.AppendLine($"- Prompt - {cmd.Name}{aliasText}");
        }

        output.AppendLine();
        output.AppendLine("Usage: Select a command to see detailed help");
        output.AppendLine("Action Commands: Execute immediately");
        output.AppendLine("Prompt Commands: Submit to AI assistant");

        return CommandResult.Ok(output.ToString());
    }

    private CommandResult ShowTraditionalHelp()
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

        return CommandResult.Ok(helpText);
    }
}