namespace duo_code.Services;

public static class ToolAnalytics
{
    private static readonly string StatsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".duocode",
        "tool_usage_stats.txt"
    );

    public static string StatsFilePathTxt => Path.ChangeExtension(StatsFilePath, ".txt");

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

    private static List<ToolUsageEntry> _usageLog = new();

    public class ToolUsageEntry
    {
        public string ToolName { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public bool Success { get; set; } = true;
    }

    public class ToolAnalyticsSummaryEntry
    {
        public int Total { get; set; }
        public int Failed { get; set; }
    }

    public class ToolAnalyticsData
    {
        public Dictionary<string, ToolAnalyticsSummaryEntry> Summary { get; set; } = new();
        public List<ToolUsageEntry> Log { get; set; } = new();
    }

    public static void LogToolUsage(string toolName, bool success)
    {
        _usageLog.Add(new ToolUsageEntry
        {
            ToolName = toolName,
            Timestamp = DateTime.UtcNow,
            Success = success
        });

        SaveStatsToFile();
    }

    public static Dictionary<string, int> GetUsageStats()
    {
        LoadStatsFromFile();
        return _usageLog
            .GroupBy(e => e.ToolName)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    public static List<string> GetMostUsedTools(int count = 5)
    {
        return _usageLog
            .GroupBy(e => e.ToolName)
            .OrderByDescending(kv => kv.Count())
            .Take(count)
            .Select(kv => kv.Key)
            .ToList();
    }

    public static int GetFailuresForTool(string toolName)
    {
        return _usageLog
            .Where(e => e.ToolName == toolName && !e.Success)
            .Count();
    }

    public static void SaveStatsToFile()
    {
        try
        {
            var lines = new List<string>();

            // 🔼 Secțiune: Rezumat
            lines.Add("📊 Summary by Tool:");
            var summary = _usageLog
                .GroupBy(e => e.ToolName)
                .OrderByDescending(g => g.Count());

            foreach (var group in summary)
            {
                lines.Add($"- {group.Key}: {group.Count()} uses");
            }

            lines.Add(""); // Linie goală între rezumat și detalii

            // 📋 Secțiune: Detalii per acțiune
            lines.Add("📄 Detailed Usage Log:");
            foreach (var entry in _usageLog)
            {
                lines.Add($"{entry.ToolName} | {entry.Timestamp:u} | Success: {entry.Success}");
            }

            // Creează directorul dacă nu există
            var directory = Path.GetDirectoryName(StatsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllLines(StatsFilePath, lines);
            Console.WriteLine($"✅ Tool stats saved to: {StatsFilePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to save stats: {ex.Message}");
        }
    }

    private static void LoadStatsFromFile()
    {
        if (!File.Exists(StatsFilePath))
        {
            _usageLog = new List<ToolUsageEntry>();
            return;
        }

        var lines = File.ReadAllLines(StatsFilePath);
        var result = new List<ToolUsageEntry>();

        foreach (var line in lines)
        {
            var parts = line.Split('|');
            if (parts.Length != 3) continue;

            var name = parts[0].Trim();
            var timeStr = parts[1].Trim();
            var successStr = parts[2].Trim().Replace("Success: ", "");

            if (!DateTime.TryParse(timeStr, out var timestamp)) continue;
            if (!bool.TryParse(successStr, out var success)) continue;

            result.Add(new ToolUsageEntry
            {
                ToolName = name,
                Timestamp = timestamp,
                Success = success
            });
        }

        _usageLog = result;
    }
}