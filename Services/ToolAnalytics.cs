using System.Text.Json;
using System.Text.Json.Serialization;

namespace duo_code.Services;

public static class ToolAnalytics
{
    private static readonly string StatsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".duocode",
        "tool_usage_stats.txt"
    );

    private static readonly string FailureStatsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".duocode",
        "tool_failure_stats.txt"
    );

    private static readonly string UsageLogFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".duocode",
        "tool_usage_log.txt"
    );

    private static readonly string FailureLogFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".duocode",
        "tool_failure_log.txt"
    );

    // These dictionaries represent persistent stats (loaded from stats files)
    private static Dictionary<string, int> _usageStats = new();
    private static Dictionary<string, int> _failureStats = new();
    
    static ToolAnalytics()
    {
        // Ensure the .duocode directory exists
        var directory = Path.GetDirectoryName(StatsFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        // Load existing stats from files
        LoadStatsFromFiles();
    }

    public static void LogToolUsage(string toolName)
    {
        // Update persistent stats
        if (_usageStats.ContainsKey(toolName))
            _usageStats[toolName]++;
        else
            _usageStats[toolName] = 1;

        // Append to log file (for admin purposes only)
        var newLogEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}|{toolName}|success";
        AppendToUsageLogFile(newLogEntry);

        // Save updated stats to file
        SaveStatsToFile();
    }

    public static void LogToolFailure(string toolName)
    {
        // Update persistent failure stats
        if (_failureStats.ContainsKey(toolName))
            _failureStats[toolName]++;
        else
            _failureStats[toolName] = 1;

        // Append to log file (for admin purposes only)
        var newLogEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}|{toolName}|failed";
        AppendToFailureLogFile(newLogEntry);

        // Save updated failure stats to file
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

    // Load stats from persistent files (not logs)
    private static void LoadStatsFromFiles()
    {
        _usageStats = LoadUsageStatsFromFile();
        _failureStats = LoadFailureStatsFromFile();
    }

    private static Dictionary<string, int> LoadUsageStatsFromFile()
    {
        var stats = new Dictionary<string, int>();
        
        if (!File.Exists(StatsFilePath))
            return stats;

        try
        {
            var lines = File.ReadAllLines(StatsFilePath);
            foreach (var line in lines)
            {
                // Skip comments and empty lines
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.StartsWith("Total Usage:"))
                    continue;

                // Parse format: "  • toolName:"
                if (line.Trim().StartsWith("• ") && line.EndsWith(":"))
                {
                    var toolName = line.Trim().Substring(2, line.Trim().Length - 3);
                    // Read next line to get usage count
                    var nextLineIndex = Array.IndexOf(lines, line) + 1;
                    if (nextLineIndex < lines.Length)
                    {
                        var dataLine = lines[nextLineIndex];
                        if (dataLine.Contains("Uses:"))
                        {
                            var usesStr = dataLine.Split("Uses:")[1].Split(';')[0].Trim();
                            if (int.TryParse(usesStr, out int uses))
                            {
                                stats[toolName] = uses;
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // Return empty stats if file reading fails
        }

        return stats;
    }

    private static Dictionary<string, int> LoadFailureStatsFromFile()
    {
        var stats = new Dictionary<string, int>();
        
        if (!File.Exists(FailureStatsFilePath))
            return stats;

        try
        {
            var lines = File.ReadAllLines(FailureStatsFilePath);
            foreach (var line in lines)
            {
                // Skip comments and empty lines
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.StartsWith("Total Failures:"))
                    continue;

                // Parse format: "  • toolName:"
                if (line.Trim().StartsWith("• ") && line.EndsWith(":"))
                {
                    var toolName = line.Trim().Substring(2, line.Trim().Length - 3);
                    // Read next line to get failure count
                    var nextLineIndex = Array.IndexOf(lines, line) + 1;
                    if (nextLineIndex < lines.Length)
                    {
                        var dataLine = lines[nextLineIndex];
                        if (dataLine.Contains("Failures:"))
                        {
                            var failuresStr = dataLine.Split("Failures:")[1].Split(';')[0].Trim();
                            if (int.TryParse(failuresStr, out int failures))
                            {
                                stats[toolName] = failures;
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // Return empty stats if file reading fails
        }

        return stats;
    }

    private static void SaveStatsToFile()
    {
        var lines = new List<string>();
        lines.Add($"# Tool Usage Statistics - Generated on {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        lines.Add("");

        var totalUsage = _usageStats.Values.Sum();
        if (totalUsage == 0)
        {
            lines.Add("No usage data available.");
        }
        else
        {
            foreach (var kvp in _usageStats.OrderByDescending(x => x.Value))
            {
                var toolName = kvp.Key;
                var uses = kvp.Value;
                var failures = _failureStats.ContainsKey(toolName) ? _failureStats[toolName] : 0;
                var totalAttempts = uses + failures;
                var failureRate = totalAttempts > 0 ? (double)failures / totalAttempts * 100 : 0.0;
                var successRate = 100.0 - failureRate;

                lines.Add($"  • {toolName}:");
                lines.Add($"    - Uses: {uses} ; Failures: {failures} ; Failure Rate: {failureRate:F1}% ; Success: {successRate:F1}%");
            }

            lines.Add("");
            lines.Add($"Total Usage: {totalUsage}");
        }

        File.WriteAllLines(StatsFilePath, lines);
    }

    private static void SaveFailureStatsToFile()
    {
        var lines = new List<string>();
        lines.Add($"# Tool Failure Statistics - Generated on {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        lines.Add("");

        var totalFailures = _failureStats.Values.Sum();
        if (totalFailures == 0)
        {
            lines.Add("No failure data available.");
        }
        else
        {
            foreach (var kvp in _failureStats.OrderByDescending(x => x.Value))
            {
                var toolName = kvp.Key;
                var failures = kvp.Value;
                var uses = _usageStats.ContainsKey(toolName) ? _usageStats[toolName] : 0;
                var totalAttempts = uses + failures;
                var failureRate = totalAttempts > 0 ? (double)failures / totalAttempts * 100 : 0.0;
                var successRate = 100.0 - failureRate;

                lines.Add($"  • {toolName}:");
                lines.Add($"    - Uses: {uses} ; Failures: {failures} ; Failure Rate: {failureRate:F1}% ; Success: {successRate:F1}%");
            }

            lines.Add("");
            lines.Add($"Total Failures: {totalFailures}");
        }

        File.WriteAllLines(FailureStatsFilePath, lines);
    }

    private static void AppendToUsageLogFile(string logEntry)
    {
        try
        {
            // Check if file exists and has header, if not create it
            if (!File.Exists(UsageLogFilePath))
            {
                var headerLines = new List<string>
                {
                    $"# Tool Usage Log - Created on {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                    "# Format: Timestamp|ToolName|Status",
                    ""
                };
                File.WriteAllLines(UsageLogFilePath, headerLines);
            }

            // Append the new log entry (this preserves all previous entries)
            File.AppendAllLines(UsageLogFilePath, new[] { logEntry });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to append to usage log: {ex.Message}");
        }
    }

    private static void AppendToFailureLogFile(string logEntry)
    {
        try
        {
            if (!File.Exists(FailureLogFilePath))
            {
                var headerLines = new List<string>
                {
                    $"# Tool Failure Log - Created on {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                    "# Format: Timestamp|ToolName|Status",
                    ""
                };
                File.WriteAllLines(FailureLogFilePath, headerLines);
            }

            File.AppendAllLines(FailureLogFilePath, new[] { logEntry });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to append to failure log: {ex.Message}");
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

    public static double GetToolUsagePercentage(string toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName)) return 0.0;

        var totalUsage = _usageStats.Values.Sum();
        if (totalUsage == 0) return 0.0;

        var toolUsage = _usageStats.ContainsKey(toolName) ? _usageStats[toolName] : 0;
        return (double)toolUsage / totalUsage * 100;
    }

    public static double GetToolFailurePercentage(string toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName)) return 0.0;

        var totalFailures = _failureStats.Values.Sum();
        if (totalFailures == 0) return 0.0;

        var toolFailures = _failureStats.ContainsKey(toolName) ? _failureStats[toolName] : 0;
        return (double)toolFailures / totalFailures * 100;
    }

    public static void ClearStats()
    {
        _usageStats.Clear();
        _failureStats.Clear();
    }
}