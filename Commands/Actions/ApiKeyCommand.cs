using duo_code.Commands.Core;
using duo_code.Services;

namespace duo_code.Commands.Actions;

public class ApiKeyCommand : ICommand
{
    public string Name => "apikey";
    public string Description => "Manage API keys";
    public CommandType Type => CommandType.Action;

    public async Task<CommandResult> ExecuteAsync(string[] args)
    {
        // Check if running in headless mode
        var isHeadless = Environment.GetCommandLineArgs().Contains("--headless");

        if (args.Length == 0)
        {
            if (isHeadless)
            {
                return ShowInteractiveApiKeyMenu();
            }
            else
            {
                return CommandResult.Ok(@"API Key Management Commands:
  /apikey reset    - Reset and re-enter API key
  /apikey show     - Show current API key location
  /apikey help     - Show this help");
            }
        }

        var action = args[0].ToLower();

        switch (action)
        {
            case "reset":
                return await ResetApiKey();

            case "show":
                return ShowApiKeyInfo();

            case "help":
                return CommandResult.Ok(@"API Key Management:
  /apikey reset    - Delete saved API key and prompt for new one
  /apikey show     - Show API key file locations
  
To get a Cerebras API key, visit: https://inference.cerebras.ai/
To get a Gemini API key, visit: https://makersuite.google.com/app/apikey");

            default:
                return CommandResult.Error($"Unknown action: {action}. Use '/apikey help' for available actions.");
        }
    }

    private CommandResult ShowInteractiveApiKeyMenu()
    {
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var cerebrasKeyPath = Path.Combine(homeDir, ".cerebras_api_key");
        var geminiKeyPath = Path.Combine(homeDir, ".gemini_api_key");

        var output = new System.Text.StringBuilder();
        output.AppendLine("🔑 API Key Management:");
        output.AppendLine();
        output.AppendLine("Current Model: API Key Manager");
        output.AppendLine();
        output.AppendLine("Available Models:");

        // Show available actions as selectable options
        output.AppendLine($"- Actions - show {(File.Exists(cerebrasKeyPath) || File.Exists(geminiKeyPath) ? "(has-keys)" : "(no-keys)")}");
        output.AppendLine($"- Actions - reset {(File.Exists(cerebrasKeyPath) || File.Exists(geminiKeyPath) ? "(can-reset)" : "(nothing-to-reset)")}");
        output.AppendLine("- Actions - help (info)");

        output.AppendLine();
        output.AppendLine("Usage: /apikey show");
        output.AppendLine("       /apikey reset");
        output.AppendLine("       /apikey help");

        return CommandResult.Ok(output.ToString());
    }

    private async Task<CommandResult> ResetApiKey()
    {
        try
        {
            // Delete existing API key files
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var cerebrasKeyPath = Path.Combine(homeDir, ".cerebras_api_key");
            var geminiKeyPath = Path.Combine(homeDir, ".gemini_api_key");

            var removedKeys = new List<string>();

            if (File.Exists(cerebrasKeyPath))
            {
                File.Delete(cerebrasKeyPath);
                removedKeys.Add("Cerebras");
            }

            if (File.Exists(geminiKeyPath))
            {
                File.Delete(geminiKeyPath);
                removedKeys.Add("Gemini");
            }

            if (removedKeys.Any())
            {
                return CommandResult.Ok($"API keys removed: {string.Join(", ", removedKeys)}. You will be prompted for new keys on next API call.");
            }
            else
            {
                return CommandResult.Ok("No API keys found to remove.");
            }
        }
        catch (Exception ex)
        {
            return CommandResult.Error($"Error resetting API keys: {ex.Message}");
        }
    }

    private CommandResult ShowApiKeyInfo()
    {
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var cerebrasKeyPath = Path.Combine(homeDir, ".cerebras_api_key");
        var geminiKeyPath = Path.Combine(homeDir, ".gemini_api_key");

        var output = new System.Text.StringBuilder();
        output.AppendLine("🔑 API Key Status:");
        output.AppendLine();
        output.AppendLine($"Cerebras: {cerebrasKeyPath}");
        output.AppendLine($"  Status: {(File.Exists(cerebrasKeyPath) ? "✅ [EXISTS]" : "❌ [NOT FOUND]")}");
        output.AppendLine();
        output.AppendLine($"Gemini: {geminiKeyPath}");
        output.AppendLine($"  Status: {(File.Exists(geminiKeyPath) ? "✅ [EXISTS]" : "❌ [NOT FOUND]")}");
        output.AppendLine();
        output.AppendLine("🌐 To get API keys:");
        output.AppendLine("Cerebras: https://inference.cerebras.ai/");
        output.AppendLine("Gemini: https://makersuite.google.com/app/apikey");

        return CommandResult.Ok(output.ToString());
    }
}