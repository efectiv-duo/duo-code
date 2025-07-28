namespace duo_code.Services;

public static class ApiKeyManager
{
    // Load API key from persistent storage
    public static string LoadApiKey()
    {
        return GetCerebrasApiKey();
    }

    public static string GetCerebrasApiKey()
    {
        try
        {
            string filePath = GetCerebrasApiKeyFilePath();
            if (File.Exists(filePath))
            {
                return File.ReadAllText(filePath).Trim();
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Warning: Could not read stored Cerebras API key: {ex.Message}");
            Console.ResetColor();
        }
        return string.Empty;
    }

    public static string GetGeminiApiKey()
    {
        try
        {
            string filePath = GetGeminiApiKeyFilePath();
            if (File.Exists(filePath))
            {
                return File.ReadAllText(filePath).Trim();
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Warning: Could not read stored Gemini API key: {ex.Message}");
            Console.ResetColor();
        }
        return string.Empty;
    }

    // Save API key to persistent storage
    public static void SaveApiKey(string key)
    {
        try
        {
            string filePath = GetApiKeyFilePath();
            File.WriteAllText(filePath, key.Trim());
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Warning: Could not save API key: {ex.Message}");
            Console.ResetColor();
        }
    }

    // Get platform-agnostic storage path
    private static string GetApiKeyFilePath()
    {
        return GetCerebrasApiKeyFilePath();
    }

    private static string GetCerebrasApiKeyFilePath()
    {
        string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(homeDirectory, ".cerebras_api_key");
    }

    private static string GetGeminiApiKeyFilePath()
    {
        string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(homeDirectory, ".gemini_api_key");
    }

    // Model and provider settings
    public static void SaveCurrentSettings(ApiProvider provider, string model)
    {
        try
        {
            string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string settingsPath = Path.Combine(homeDirectory, ".duo_code_settings");
            
            var settings = new
            {
                Provider = provider.ToString(),
                Model = model
            };
            
            string json = System.Text.Json.JsonSerializer.Serialize(settings, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(settingsPath, json);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Warning: Could not save settings: {ex.Message}");
            Console.ResetColor();
        }
    }

    public static (ApiProvider Provider, string Model) LoadCurrentSettings()
    {
        try
        {
            string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string settingsPath = Path.Combine(homeDirectory, ".duo_code_settings");
            
            if (File.Exists(settingsPath))
            {
                string json = File.ReadAllText(settingsPath);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;
                
                if (root.TryGetProperty("Provider", out var providerElement) && 
                    root.TryGetProperty("Model", out var modelElement))
                {
                    if (Enum.TryParse<ApiProvider>(providerElement.GetString(), out var provider))
                    {
                        return (provider, modelElement.GetString() ?? "");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Warning: Could not load saved settings: {ex.Message}");
            Console.ResetColor();
        }
        
        // Return defaults if loading fails
        return (ApiProvider.Cerebras, "qwen-3-235b-a22b");
    }
}