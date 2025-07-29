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
        string duocodeDir = Path.Combine(homeDirectory, ".duocode");
        
        // Ensure directory exists
        if (!Directory.Exists(duocodeDir))
        {
            Directory.CreateDirectory(duocodeDir);
        }
        
        // Check new location first
        string newPath = Path.Combine(duocodeDir, "cerebras_api_key");
        if (File.Exists(newPath))
        {
            return newPath;
        }
        
        // Check old location and migrate if found
        string oldPath = Path.Combine(homeDirectory, ".cerebras_api_key");
        if (File.Exists(oldPath))
        {
            try
            {
                string key = File.ReadAllText(oldPath).Trim();
                File.WriteAllText(newPath, key);
                File.Delete(oldPath);
            }
            catch { }
        }
        
        return newPath;
    }

    private static string GetGeminiApiKeyFilePath()
    {
        string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string duocodeDir = Path.Combine(homeDirectory, ".duocode");
        
        // Ensure directory exists
        if (!Directory.Exists(duocodeDir))
        {
            Directory.CreateDirectory(duocodeDir);
        }
        
        // Check new location first
        string newPath = Path.Combine(duocodeDir, "gemini_api_key");
        if (File.Exists(newPath))
        {
            return newPath;
        }
        
        // Check old location and migrate if found
        string oldPath = Path.Combine(homeDirectory, ".gemini_api_key");
        if (File.Exists(oldPath))
        {
            try
            {
                string key = File.ReadAllText(oldPath).Trim();
                File.WriteAllText(newPath, key);
                File.Delete(oldPath);
            }
            catch { }
        }
        
        return newPath;
    }

    // Model and provider settings
    public static void SaveCurrentSettings(ApiProvider provider, string model)
    {
        try
        {
            string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string duocodeDir = Path.Combine(homeDirectory, ".duocode");
            
            // Ensure the .duocode directory exists
            if (!Directory.Exists(duocodeDir))
            {
                Directory.CreateDirectory(duocodeDir);
            }
            
            string settingsPath = Path.Combine(duocodeDir, "model_settings.json");
            
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
            string duocodeDir = Path.Combine(homeDirectory, ".duocode");
            string settingsPath = Path.Combine(duocodeDir, "model_settings.json");
            
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
            
            // Check old location for backward compatibility
            string oldSettingsPath = Path.Combine(homeDirectory, ".duo_code_settings");
            if (File.Exists(oldSettingsPath))
            {
                string json = File.ReadAllText(oldSettingsPath);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;
                
                if (root.TryGetProperty("Provider", out var providerElement) && 
                    root.TryGetProperty("Model", out var modelElement))
                {
                    if (Enum.TryParse<ApiProvider>(providerElement.GetString(), out var provider))
                    {
                        var result = (provider, modelElement.GetString() ?? "");
                        
                        // Migrate to new location
                        SaveCurrentSettings(provider, result.Item2);
                        
                        // Delete old file
                        try { File.Delete(oldSettingsPath); } catch { }
                        
                        return result;
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