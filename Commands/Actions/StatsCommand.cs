using duo_code.Commands.Core;
using duo_code.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace duo_code.Commands.Actions
{
    public class StatsCommand : ICommand, IHasAliases
    {
        public string Name => "stats";
        public string Description => "Displays tool usage statistics and provides helpful suggestions.";
        public CommandType Type => CommandType.Action;

        public string[] GetAliases() => new[] { "s" };

        public Task<CommandResult> ExecuteAsync(string[] args)
        {
            var sb = new StringBuilder();
            var stats = ToolAnalytics.GetUsageStats();

            if (stats.Count == 0)
            {
                return Task.FromResult(CommandResult.Ok("No usage data recorded yet."));
            }

            sb.AppendLine("📊 Tool usage statistics:");
            foreach (var kvp in stats.OrderByDescending(k => k.Value))
            {
                sb.AppendLine($"- {kvp.Key}: {kvp.Value} uses");
            }

            sb.AppendLine();
            sb.AppendLine("💡 Suggestions for useful but underused tools:");

            var leastUsed = stats.OrderBy(k => k.Value)
                                 .Where(k => k.Value < 3)
                                 .Take(3)
                                 .Select(k => k.Key);

            foreach (var tool in leastUsed)
            {
                sb.AppendLine($"👉 Try using „{tool}” more often. It can be helpful in certain workflows.");
            }

            return Task.FromResult(CommandResult.Ok(sb.ToString()));
        }
    }
}