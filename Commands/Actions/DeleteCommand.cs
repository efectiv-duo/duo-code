using duo_code.Commands.Core;
using System.IO;
using duo_code.Services;

namespace duo_code.Commands.Actions;

public class DeleteCommand : ICommand, IHasAliases
{
    public string Name => "delete";
    public string Description => "Clear the stats";
    public CommandType Type => CommandType.Action;
    public string[] GetAliases() => new[] { "del" };

    private static string StatsFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".duocode",
        "tool_usage_stats.txt"
    );

    private static string FailureStatsFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".duocode",
        "tool_failure_stats.txt"
    );

    public async Task<CommandResult> ExecuteAsync(string[] args)
    {
        try
        {
            bool statsExists = File.Exists(StatsFilePath);
            bool failureStatsExists = File.Exists(FailureStatsFilePath);

            if (!statsExists && !failureStatsExists)
            {
                return CommandResult.Ok("No stats files found to delete.");
            }

            if (args.Length > 0 && args[0].ToLower() == "--force")
            {
                await ClearStatsFileAsync();
                ToolAnalytics.ClearStats();
                return CommandResult.Ok("Stats cleared successfully.");
            }

            bool hasContent = false;
            
            if (statsExists)
            {
                var fileContent = await File.ReadAllTextAsync(StatsFilePath);
                if (!string.IsNullOrWhiteSpace(fileContent))
                    hasContent = true;
            }

            if (failureStatsExists)
            {
                var failureContent = await File.ReadAllTextAsync(FailureStatsFilePath);
                if (!string.IsNullOrWhiteSpace(failureContent))
                    hasContent = true;
            }

            if (!hasContent)
            {
                return CommandResult.Ok("Stats files are already empty.");
            }

            await ClearStatsFileAsync();
            ToolAnalytics.ClearStats();
            return CommandResult.Ok("Stats cleared successfully.");
        }
        catch (Exception ex)
        {
            return CommandResult.Error($"Unexpected error: {ex.Message}");
        }
    }

    private async Task ClearStatsFileAsync()
    {
        var directory = Path.GetDirectoryName(StatsFilePath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(StatsFilePath, string.Empty);
        await File.WriteAllTextAsync(FailureStatsFilePath, string.Empty);
    }
}