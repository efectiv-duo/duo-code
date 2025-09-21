using duo_code.Commands.Core;
using duo_code.Services;
using System;
using System.IO;
using System.Threading.Tasks;

namespace duo_code.Commands.Actions
{
    public class DeleteStatsCommand : ICommand, IHasAliases
    {
        public string Name => "statsdelete";
        public string Description => "Deletes all tool usage statistics from the stats file.";
        public CommandType Type => CommandType.Action;

        public string[] GetAliases() => new[] { "sdel" };

        public Task<CommandResult> ExecuteAsync(string[] args)
        {
            try
            {
                var statsFilePath = ToolAnalytics.StatsFilePathTxt;

                if (File.Exists(statsFilePath))
                {
                    File.WriteAllText(statsFilePath, string.Empty);
                    return Task.FromResult(CommandResult.Ok("🗑️ Tool usage statistics have been deleted."));
                }
                else
                {
                    return Task.FromResult(CommandResult.Ok("⚠️ Stats file does not exist. Nothing to delete."));
                }
            }
            catch (Exception ex)
            {
                return Task.FromResult(CommandResult.Error($"❌ Failed to delete stats: {ex.Message}"));
            }
        }
    }
}