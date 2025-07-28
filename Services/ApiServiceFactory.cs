namespace duo_code.Services;

public static class ApiServiceFactory
{
    public static IApiService CreateApiService(ApiProvider provider)
    {
        return provider switch
        {
            ApiProvider.Cerebras => new CerebrasApiService(GetOrPromptForApiKey(provider)),
            ApiProvider.Gemini => new GeminiApiService(GetOrPromptForApiKey(provider)),
            _ => throw new ArgumentException($"Unsupported API provider: {provider}")
        };
    }

    private static string GetOrPromptForApiKey(ApiProvider provider)
    {
        string apiKey = provider switch
        {
            ApiProvider.Cerebras => ApiKeyManager.GetCerebrasApiKey(),
            ApiProvider.Gemini => ApiKeyManager.GetGeminiApiKey(),
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Please enter your {provider} API key:");
            Console.ResetColor();
            
            apiKey = Console.ReadLine()?.Trim() ?? string.Empty;
            
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                SaveApiKey(provider, apiKey);
            }
            else
            {
                throw new InvalidOperationException($"API key is required for {provider} provider");
            }
        }

        return apiKey;
    }

    private static void SaveApiKey(ApiProvider provider, string apiKey)
    {
        try
        {
            string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string filePath = provider switch
            {
                ApiProvider.Cerebras => Path.Combine(homeDirectory, ".cerebras_api_key"),
                ApiProvider.Gemini => Path.Combine(homeDirectory, ".gemini_api_key"),
                _ => throw new ArgumentException($"Unsupported provider: {provider}")
            };
            
            File.WriteAllText(filePath, apiKey);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"{provider} API key saved successfully.");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Warning: Could not save {provider} API key: {ex.Message}");
            Console.ResetColor();
        }
    }
}