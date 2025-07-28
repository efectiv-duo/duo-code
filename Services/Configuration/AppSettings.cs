using System.Collections.Generic;

namespace duo_code.Services.Configuration
{
    public class AppSettings
    {
        public ApiSettings Api { get; set; } = new();
        public ModelSettings Models { get; set; } = new();
        public InterfaceSettings Interface { get; set; } = new();
        public ToolSettings Tools { get; set; } = new();
    }

    public class ApiSettings
    {
        public string BaseUrl { get; set; } = "https://api.cerebras.ai/v1";
        public int TimeoutSeconds { get; set; } = 300;
        public string ApiKeyPath { get; set; } = "~/.cerebras-api-key";
    }

    public class ModelSettings
    {
        public string DefaultModel { get; set; } = "qwen-3-235b-a22b";
        public List<string> AvailableModels { get; set; } = new()
        {
            "qwen-3-235b-a22b",
            "qwen-3-32b",
            "llama-4-maverick-17b-128e-instruct",
            "llama-4-scout-17b-16e-instruct",
            "deepseek-r1-distill-llama-70b"
        };
    }

    public class InterfaceSettings
    {
        public ConsoleColors Colors { get; set; } = new();
        public bool ShowThinkingIndicator { get; set; } = true;
        public int MaxMessageHistoryDisplay { get; set; } = 10;
    }

    public class ConsoleColors
    {
        public string User { get; set; } = "White";
        public string Assistant { get; set; } = "Yellow";
        public string Tool { get; set; } = "Cyan";
        public string Error { get; set; } = "Red";
        public string Info { get; set; } = "Gray";
    }

    public class ToolSettings
    {
        public bool EnableJsonFormat { get; set; } = true;
        public bool EnableLegacyFormat { get; set; } = true;
        public int MaxFileReadSize { get; set; } = 1_000_000; // 1MB
    }
}