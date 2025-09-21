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
        if (args.Length == 0)
        {
            return CommandResult.Ok(@"API Key Management Commands:
  /apikey reset    - Reset and re-enter API key
  /apikey show     - Show current API key location
  /apikey help     - Show this help");
        }

        var action = args[0].ToLower();

        switch (action)
        {
            case "reset":
                await ResetApiKey();
                return CommandResult.Ok("API key reset. You will be prompted for a new key on next API call.");

            case "show":
                ShowApiKeyInfo();
                return CommandResult.Ok("");

            case "help":
                return CommandResult.Ok(@"API Key Management:
  /apikey reset    - Delete saved API key and prompt for new one
  /apikey show     - Show API key file locations
  
To get a Cerebras API key, visit: https://inference.cerebras.ai/
To get a Gemini API key, visit: https://makersuite.google.com/app/apikey
To get a OpenAI API key, visit: https://platform.openai.com/api-keys");

            default:
                return CommandResult.Error($"Unknown action: {action}. Use '/apikey help' for available actions.");
        }
    }

    private async Task ResetApiKey()
    {
        try
        {
            // Delete existing API key files
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var cerebrasKeyPath = Path.Combine(homeDir, ".cerebras_api_key");
            var geminiKeyPath = Path.Combine(homeDir, ".gemini_api_key");
            var openaiKeyPath = Path.Combine(homeDir, ".openai_api_key");

            if (File.Exists(cerebrasKeyPath))
            {
                File.Delete(cerebrasKeyPath);
                Console.WriteLine("Cerebras API key removed.");
            }

            if (File.Exists(geminiKeyPath))
            {
                File.Delete(geminiKeyPath);
                Console.WriteLine("Gemini API key removed.");
            }

            if (File.Exists(openaiKeyPath))
            {
                File.Delete(openaiKeyPath);
                Console.WriteLine("OpenAI API key removed.");
            }

            if (!File.Exists(cerebrasKeyPath) && !File.Exists(geminiKeyPath))
            {
                Console.WriteLine("No API keys found to remove.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error resetting API keys: {ex.Message}");
        }
    }

    private void ShowApiKeyInfo()
    {
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var cerebrasKeyPath = Path.Combine(homeDir, ".cerebras_api_key");
        var geminiKeyPath = Path.Combine(homeDir, ".gemini_api_key");
        var openaiKeyPath = Path.Combine(homeDir, ".openai_api_key");

        Console.WriteLine("API Key Locations:");
        Console.WriteLine($"Cerebras: {cerebrasKeyPath} {(File.Exists(cerebrasKeyPath) ? "[EXISTS]" : "[NOT FOUND]")}");
        Console.WriteLine($"Gemini:   {geminiKeyPath} {(File.Exists(geminiKeyPath) ? "[EXISTS]" : "[NOT FOUND]")}");
        Console.WriteLine($"OpenAI:   {openaiKeyPath} {(File.Exists(openaiKeyPath) ? "[EXISTS]" : "[NOT FOUND]")}");
        Console.WriteLine();
        Console.WriteLine("To get API keys:");
        Console.WriteLine("Cerebras: https://inference.cerebras.ai/");
        Console.WriteLine("Gemini:   https://makersuite.google.com/app/apikey");
        Console.WriteLine("OpenAI:   https://platform.openai.com/api-keys");
    }
}