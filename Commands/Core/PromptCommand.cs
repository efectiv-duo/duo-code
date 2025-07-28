namespace duo_code.Commands.Core;

public class PromptCommand : ICommand
{
    private readonly string _filePath;
    private readonly string _promptContent;
    
    public string Name { get; }
    public string Description { get; }
    public CommandType Type => CommandType.Prompt;
    
    public PromptCommand(string filePath)
    {
        _filePath = filePath;
        Name = Path.GetFileNameWithoutExtension(filePath).ToLower();
        
        // Read the file content
        _promptContent = File.ReadAllText(filePath);
        
        // Extract description from first line if it's a comment
        var lines = _promptContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length > 0 && lines[0].StartsWith("<!--") && lines[0].EndsWith("-->"))
        {
            Description = lines[0].Substring(4, lines[0].Length - 7).Trim();
        }
        else
        {
            Description = $"Prompt command from {Path.GetFileName(filePath)}";
        }
    }
    
    public Task<CommandResult> ExecuteAsync(string[] args)
    {
        // Replace any placeholders in the prompt with arguments
        var prompt = _promptContent;
        
        // Simple placeholder replacement: {0}, {1}, etc.
        for (int i = 0; i < args.Length; i++)
        {
            prompt = prompt.Replace($"{{{i}}}", args[i]);
        }
        
        // Remove the description comment if present
        var lines = prompt.Split('\n');
        if (lines.Length > 0 && lines[0].StartsWith("<!--") && lines[0].EndsWith("-->"))
        {
            prompt = string.Join('\n', lines.Skip(1)).Trim();
        }
        
        return Task.FromResult(CommandResult.Prompt(prompt));
    }
}