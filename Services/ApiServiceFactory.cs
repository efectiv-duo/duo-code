namespace duo_code.Services;

public static class ApiServiceFactory
{
    public static IApiService CreateApiService(ApiProvider provider)
    {
        return provider switch
        {
            ApiProvider.Cerebras => new ApiService(GetOrPromptForApiKey(provider)),
            ApiProvider.Gemini => new GeminiApiService(GetOrPromptForApiKey(provider)),
            ApiProvider.Anthropic => new AnthropicApiService(GetOrPromptForApiKey(provider)),
            ApiProvider.OpenAI => new OpenAiApiService(GetOrPromptForApiKey(provider)),
            _ => throw new ArgumentException($"Unsupported API provider: {provider}")
        };
    }

    private static string GetOrPromptForApiKey(ApiProvider provider)
    {
        string apiKey = provider switch
        {
            ApiProvider.Cerebras => ApiKeyManager.GetApiKey(provider),
            ApiProvider.Gemini => ApiKeyManager.GetApiKey(provider),
            ApiProvider.OpenAI => ApiKeyManager.GetApiKey(provider),
            ApiProvider.Anthropic => ApiKeyManager.GetApiKey(provider),
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            WriteWarning($"Please enter your {provider} API key:");
            
            apiKey = Console.ReadLine()?.Trim() ?? string.Empty;
            
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                ApiKeyManager.SaveApiKey(apiKey, provider);
            }
            else
            {
                throw new InvalidOperationException($"API key is required for {provider} provider");
            }
        }

        return apiKey;
    }
}