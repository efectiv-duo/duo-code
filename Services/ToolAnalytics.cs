using System.Text.Json;
using System.Text.Json.Serialization;

namespace duo_code.Services;

public static class ToolAnalytics
{
    private static readonly string StatsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".duocode",
        "tool_usage_stats.json"
    );
    private static Dictionary<string, int> _usageStats = new();

    static ToolAnalytics()
    {
        // Ensure the .duocode directory exists
        var directory = Path.GetDirectoryName(StatsFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        LoadStatsFromFile();
    }

    public static void LogToolUsage(string toolName)
    {
        if (_usageStats.ContainsKey(toolName))
            _usageStats[toolName]++;
        else
            _usageStats[toolName] = 1;

        SaveStatsToFile();
    }

    public static Dictionary<string, int> GetUsageStats()
    {
        return new Dictionary<string, int>(_usageStats);
    }

    public static List<string> GetMostUsedTools(int count = 5)
    {
        return _usageStats
            .OrderByDescending(kv => kv.Value)
            .Take(count)
            .Select(kv => kv.Key)
            .ToList();
    }

    public static void SaveStatsToFile()
    {
        var json = JsonSerializer.Serialize(_usageStats, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(StatsFilePath, json);
    }

    private static void LoadStatsFromFile()
    {
        if (File.Exists(StatsFilePath))
        {
            var json = File.ReadAllText(StatsFilePath);
            _usageStats = JsonSerializer.Deserialize<Dictionary<string, int>>(json)
                          ?? new Dictionary<string, int>();
        }
    }
}