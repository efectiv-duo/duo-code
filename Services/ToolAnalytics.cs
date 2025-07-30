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

    private static Dictionary<string, int> _usageStats = new();
    private static Dictionary<string, int> _failureStats = new();
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
        LoadUsageLogFromFile();
        LoadFailureLogFromFile();
    }

    public static void LogToolUsage(string toolName)
    {
        if (_usageStats.ContainsKey(toolName))
            _usageStats[toolName]++;
        else
            _usageStats[toolName] = 1;

        _usageLog.Add($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}|{toolName}|success");

        SaveStatsToFile();
        SaveUsageLogToFile();
    }

    public static void LogToolFailure(string toolName)
    {
        if (_failureStats.ContainsKey(toolName))
            _failureStats[toolName]++;
        else
            _failureStats[toolName] = 1;

        _failureLog.Add($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}|{toolName}|failed");

        SaveFailureStatsToFile();
        SaveFailureLogToFile();
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
                var failures = GetToolFailureCount(toolName);
                var failureRate = uses + failures > 0 ? (double)failures / (uses + failures) * 100 : 0.0;
                var successRate = 100.0 - failureRate;

                lines.Add($"  • {toolName}:");
                lines.Add($"    - Uses: {uses} ; Failures: {failures} ; Failure Rate: {failureRate:F1}% ; Success: {successRate:F1}%");
            }
            
            lines.Add("");
            lines.Add($"Total Usage: {totalUsage}");
        }

        File.WriteAllLines(StatsFilePath, lines);
    }

    private static void LoadStatsFromFile()
    {
        if (File.Exists(StatsFilePath))
        {
            try
            {
                var lines = File.ReadAllLines(StatsFilePath);
                _usageStats.Clear();

                foreach (var line in lines)
                {
                    // Skip comments and empty lines
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;

                    // Parse the new format: - Uses: X | Failures: Y | ...
                    if (line.Trim().StartsWith("- Uses:"))
                    {
                        // Find the corresponding tool name from the previous line
                        var lineIndex = Array.IndexOf(lines, line);
                        if (lineIndex > 0)
                        {
                            var toolLine = lines[lineIndex - 1].Trim();
                            if (toolLine.StartsWith("") && toolLine.EndsWith(";"))
                            {
                                var toolName = toolLine.Substring(2, toolLine.Length - 3).Trim();
                                
                                // Extract uses count
                                var parts = line.Split(';');
                                if (parts.Length > 0)
                                {
                                    var usesPart = parts[0].Trim();
                                    var usesMatch = System.Text.RegularExpressions.Regex.Match(usesPart, @"Uses:\s*(\d+)");
                                    if (usesMatch.Success && int.TryParse(usesMatch.Groups[1].Value, out int uses))
                                    {
                                        _usageStats[toolName] = uses;
                                    }
                                }
                            }
                        }
                    }

                    // Also support old format for backward compatibility
                    var oldFormatParts = line.Split('=', 2);
                    if (oldFormatParts.Length == 2)
                    {
                        var toolName = oldFormatParts[0].Trim();
                        var countPart = oldFormatParts[1].Split('(')[0].Trim();
                        
                        if (int.TryParse(countPart, out int count))
                        {
                            _usageStats[toolName] = count;
                        }
                    }
                }
            }
            catch
            {
                _usageStats = new Dictionary<string, int>();
            }
        }
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
                var uses = GetToolUsageCount(toolName);
                var failureRate = uses + failures > 0 ? (double)failures / (uses + failures) * 100 : 0.0;
                var successRate = 100.0 - failureRate;

                lines.Add($"  • {toolName}:");
                lines.Add($"    - Uses: {uses} ; Failures: {failures} ; Failure Rate; {failureRate:F1}% ; Success: {successRate:F1}%");
            }
            
            lines.Add("");
            lines.Add($"Total Failures: {totalFailures}");
        }

        File.WriteAllLines(FailureStatsFilePath, lines);
    }

    private static void LoadFailureStatsFromFile()
    {
        if (File.Exists(FailureStatsFilePath))
        {
            try
            {
                var lines = File.ReadAllLines(FailureStatsFilePath);
                _failureStats.Clear();

                foreach (var line in lines)
                {
                    // Skip comments and empty lines
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;

                    // Parse the new format: - Uses: X | Failures: Y | ...
                    if (line.Trim().StartsWith("- Uses:"))
                    {
                        // Find the corresponding tool name from the previous line
                        var lineIndex = Array.IndexOf(lines, line);
                        if (lineIndex > 0)
                        {
                            var toolLine = lines[lineIndex - 1].Trim();
                            if (toolLine.StartsWith("• ") && toolLine.EndsWith(":"))
                            {
                                var toolName = toolLine.Substring(2, toolLine.Length - 3).Trim();
                                
                                // Extract failures count
                                var parts = line.Split(';');
                                if (parts.Length > 1)
                                {
                                    var failuresPart = parts[1].Trim();
                                    var failuresMatch = System.Text.RegularExpressions.Regex.Match(failuresPart, @"Failures:\s*(\d+)");
                                    if (failuresMatch.Success && int.TryParse(failuresMatch.Groups[1].Value, out int failures))
                                    {
                                        _failureStats[toolName] = failures;
                                    }
                                }
                            }
                        }
                    }

                    // Also support old format for backward compatibility
                    var oldFormatParts = line.Split('=', 2);
                    if (oldFormatParts.Length == 2)
                    {
                        var toolName = oldFormatParts[0].Trim();
                        var countPart = oldFormatParts[1].Split('(')[0].Trim();
                        
                        if (int.TryParse(countPart, out int count))
                        {
                            _failureStats[toolName] = count;
                        }
                    }
                }
            }
            catch
            {
                _failureStats = new Dictionary<string, int>();
            }
        }
    }

    private static void SaveUsageLogToFile()
    {
        var lines = new List<string>();
        lines.Add($"# Tool Usage Log - Generated on {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        lines.Add("# Format: Timestamp; ToolName; Status");
        lines.Add("");
        lines.AddRange(_usageLog);

        File.WriteAllLines(UsageLogFilePath, lines);
    }

    private static void LoadUsageLogFromFile()
    {
        if (File.Exists(UsageLogFilePath))
        {
            try
            {
                var lines = File.ReadAllLines(UsageLogFilePath);
                _usageLog.Clear();

                foreach (var line in lines)
                {
                    // Skip comments and empty lines
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;

                    _usageLog.Add(line);
                }
            }
            catch
            {
                _usageLog = new List<string>();
            }
        }
    }

    private static void SaveFailureLogToFile()
    {
        var lines = new List<string>();
        lines.Add($"# Tool Failure Log - Generated on {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        lines.Add("# Format: Timestamp; ToolName; Status");
        lines.Add("");
        lines.AddRange(_failureLog);

        File.WriteAllLines(FailureLogFilePath, lines);
    }

    private static void LoadFailureLogFromFile()
    {
        if (File.Exists(FailureLogFilePath))
        {
            try
            {
                var lines = File.ReadAllLines(FailureLogFilePath);
                _failureLog.Clear();

                foreach (var line in lines)
                {
                    // Skip comments and empty lines
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;

                    _failureLog.Add(line);
                }
            }
            catch
            {
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

    public static double GetToolUsagePercentage(string toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName)) return 0.0;
        
        var totalUsage = _usageStats.Values.Sum();
        if (totalUsage == 0) return 0.0;
        
        var toolUsage = GetToolUsageCount(toolName);
        return (double)toolUsage / totalUsage * 100;
    }

    public static double GetToolFailurePercentage(string toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName)) return 0.0;
        
        var totalFailures = _failureStats.Values.Sum();
        if (totalFailures == 0) return 0.0;
        
        var toolFailures = GetToolFailureCount(toolName);
        return (double)toolFailures / totalFailures * 100;
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