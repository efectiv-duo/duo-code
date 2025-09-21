using System;
using System.IO;
using System.Text.Json;

namespace duo_code.Services.Configuration
{
    public class LocalConfigurationService
    {
        private readonly string _configPath;
        private LocalSettings _localSettings;

        public LocalConfigurationService()
        {
            _configPath = 
                Path.Combine(
                    Constants.GlobalConfigDirectory, 
                    Constants.LOCAL_CONFIG_FILE_NAME);

            _localSettings = LoadSettings();

            SaveSettings();
        }

        public LocalSettings LocalSettings => _localSettings;

        private LocalSettings LoadSettings()
        {
            if (File.Exists(_configPath))
            {
                try
                {
                    var json = File.ReadAllText(_configPath);
                    return JsonSerializer.Deserialize<LocalSettings>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip
                    }) ?? new LocalSettings();
                }
                catch
                {
                    return new LocalSettings();
                }
            }
            else
            {
                return new LocalSettings();
            }
        }

        public void SaveSettings()
        {
            try
            {
                var json = JsonSerializer.Serialize(_localSettings, new JsonSerializerOptions
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
    }
}