using System;
using System.IO;
using System.Text.Json;

namespace duo_code.Services.Configuration
{
    public class ConfigurationService
    {
        private const string ConfigFileName = "duo-code.settings.json";
        private readonly string _configPath;
        private AppSettings _settings;

        public ConfigurationService()
        {
            _configPath = GetConfigPath();
            LoadSettings();
        }

        public AppSettings Settings => _settings;

        private string GetConfigPath()
        {
            // Check multiple locations for config file
            var locations = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), ConfigFileName),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DuoCode", ConfigFileName),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "duo-code", ConfigFileName)
            };

            foreach (var location in locations)
            {
                if (File.Exists(location))
                    return location;
            }

            // Default to current directory
            return locations[0];
        }

        private void LoadSettings()
        {
            if (File.Exists(_configPath))
            {
                try
                {
                    var json = File.ReadAllText(_configPath);
                    _settings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip
                    }) ?? new AppSettings();
                }
                catch
                {
                    _settings = new AppSettings();
                }
            }
            else
            {
                _settings = new AppSettings();
                SaveSettings();
            }
        }

        public void SaveSettings()
        {
            try
            {
                var directory = Path.GetDirectoryName(_configPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                
                File.WriteAllText(_configPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save settings: {ex.Message}");
            }
        }

        public T GetValue<T>(string key, T defaultValue = default)
        {
            // Simple key-value getter using reflection
            var parts = key.Split('.');
            object current = _settings;

            foreach (var part in parts)
            {
                if (current == null) return defaultValue;
                
                var property = current.GetType().GetProperty(part);
                if (property == null) return defaultValue;
                
                current = property.GetValue(current);
            }

            return current is T value ? value : defaultValue;
        }
    }
}