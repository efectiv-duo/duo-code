namespace duo_code.Services;

public static class ApiKeyManager
{
    public static string GetApiKey(ApiProvider apiProvider)
    {
        try
        {
            string filePath = GetApiKeyFilePath(apiProvider);
            if (File.Exists(filePath))
            {
                return File.ReadAllText(filePath).Trim();
            }
        }
        catch (Exception ex)
        {
            WriteWarning($"Warning: Could not read stored API key: {ex.Message}");
        }
        return string.Empty;
    }

    // Save API key to persistent storage
    public static void SaveApiKey(string key, ApiProvider apiProvider)
    {
        try
        {
            string filePath = GetApiKeyFilePath(apiProvider);
            File.WriteAllText(filePath, key.Trim());
        }
        catch (Exception ex)
        {
            WriteError($"Warning: Could not save API key: {ex.Message}");
        }
    }

    // Get platform-agnostic storage path
    private static string GetApiKeyFilePath(ApiProvider apiProvider)
    {
        string dir = Constants.GlobalConfigDirectory;

        // Check new location first
        string path = Path.Combine(dir, $"{apiProvider.ToString().ToLower()}_api_key");
        if (File.Exists(path))
        {
            return path;
        }

        return path;
    }

    // Model and provider settings
    public static void SaveCurrentSettings(ApiProvider provider, string model)
    {
        try
        {
            string dir = Constants.GlobalConfigDirectory;
                        
            string settingsPath = Path.Combine(dir, Constants.MODEL_CONFIG_FILE_NAME);
            
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
            WriteWarning($"Warning: Could not save settings: {ex.Message}");
        }
    }

    public static (ApiProvider Provider, string Model) LoadCurrentSettings()
    {
        try
        {
            string dir = Constants.GlobalConfigDirectory;

            string settingsPath = Path.Combine(dir, Constants.MODEL_CONFIG_FILE_NAME);
            
            // First try the new location
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
            WriteWarning($"Warning: Could not load saved settings, reverting to defaults: {ex.Message}");
        }
        
        // Return defaults if loading fails
        return (ApiProvider.Cerebras, "gemini-2.5-flash");
    }
}