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

    private static readonly string FailureStatsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".duocode",
        "tool_failure_stats.json"
    );


    private static Dictionary<string, int> _usageStats = new();
    private static Dictionary<string, int> _failureStats = new();  //dictionar pt toolurile care au dat fail
    private static List<string> _usageLog = new();
    private static List<string> _failureLog = new();
    static ToolAnalytics()
    {
        // Ensure the .duocode directory exists
        var directory = Path.GetDirectoryName(StatsFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        LoadStatsFromFile();
        LoadFailureStatsFromFile();
    }

    public static void LogToolUsage(string toolName)
    {
        if (_usageStats.ContainsKey(toolName))
            _usageStats[toolName]++;
        else
            _usageStats[toolName] = 1;

        _usageLog.Add($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}|{toolName}|success");

        SaveStatsToFile();
    }

    public static void LogToolFailure(string toolName) //
    {
        if (_failureStats.ContainsKey(toolName))
            _failureStats[toolName]++;
        else
            _failureStats[toolName] = 1;

        _failureLog.Add($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}|{toolName}|failed");

        SaveFailureStatsToFile();
    }

    public static Dictionary<string, int> GetUsageStats()
    {
        return new Dictionary<string, int>(_usageStats);
    }

    public static Dictionary<string, int> GetFailureStats()
    {
        return new Dictionary<string, int>(_failureStats);
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
        var data = new
        {
            Stats = _usageStats,
            Log = _usageLog
        };

        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(StatsFilePath, json);
    }

    private static void LoadStatsFromFile()
    {
        if (File.Exists(StatsFilePath))
        {
            try
            {
                var json = File.ReadAllText(StatsFilePath);
                var data = JsonSerializer.Deserialize<JsonElement>(json);

                if (data.TryGetProperty("Stats", out var statsElement))
                {
                    _usageStats = JsonSerializer.Deserialize<Dictionary<string, int>>(statsElement.GetRawText()) ?? new();
                }
                else
                {
                    _usageStats = JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? new();
                }

                if (data.TryGetProperty("Log", out var logElement))
                {
                    _usageLog = JsonSerializer.Deserialize<List<string>>(logElement.GetRawText()) ?? new();
                }
            }
            catch
            {
                _usageStats = new Dictionary<string, int>();
                _usageLog = new List<string>();
            }
        }
    }

    private static void SaveFailureStatsToFile()
    {
        var data = new
        {
            Stats = _failureStats,
            Log = _failureLog
        };

        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(FailureStatsFilePath, json);
    }

    private static void LoadFailureStatsFromFile()
    {
        if (File.Exists(FailureStatsFilePath))
        {
            try
            {
                var json = File.ReadAllText(FailureStatsFilePath);
                var data = JsonSerializer.Deserialize<JsonElement>(json);

                if (data.TryGetProperty("Stats", out var statsElement))
                {
                    _failureStats = JsonSerializer.Deserialize<Dictionary<string, int>>(statsElement.GetRawText()) ?? new();
                }
                else
                {
                    _failureStats = JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? new();
                }

                if (data.TryGetProperty("Log", out var logElement))
                {
                    _failureLog = JsonSerializer.Deserialize<List<string>>(logElement.GetRawText()) ?? new();
                }
            }
            catch
            {
                _failureStats = new Dictionary<string, int>();
                _failureLog = new List<string>();
            }
        }
    }

    public static int GetToolFailureCount(string toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName)) return 0;
        return _failureStats.ContainsKey(toolName) ? _failureStats[toolName] : 0;
    }

    public static int GetToolUsageCount(string toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName)) return 0;
        return _usageStats.ContainsKey(toolName) ? _usageStats[toolName] : 0;
    }

   /* 
   public static List<string> GetUsageLog()
    {
        return new List<string>(_usageLog);
    }

    public static List<string> GetFailureLog()
    {
        return new List<string>(_failureLog);
    }
    */

}