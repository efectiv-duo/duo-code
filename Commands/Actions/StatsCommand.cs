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
            var failureStats = ToolAnalytics.GetFailureStats();

            if (stats.Count == 0)
            {
                return Task.FromResult(CommandResult.Ok("No usage data recorded yet."));
            }

            var totalUses = stats.Values.Sum();
            var totalFailures = failureStats.Values.Sum();
            var overallFailureRate = totalUses > 0 ? ((double)totalFailures / totalUses * 100) : 0;
            var overallSuccessRate = 100 - overallFailureRate;

            sb.AppendLine("📈 Overall Performance:");
            sb.AppendLine($"  • Total tool uses: {totalUses}");
            sb.AppendLine($"  • Total failures: {totalFailures}");
            sb.AppendLine($"  • Failure rate: {overallFailureRate:F1}%");
            sb.AppendLine($"  • Success rate: {overallSuccessRate:F1}%");
            sb.AppendLine();

            sb.AppendLine("📊 Tool usage statistics:");
            foreach (var kvp in stats.OrderByDescending(k => k.Value))
            {
                //sb.AppendLine($"- {kvp.Key}: {kvp.Value} uses");
                var toolName = kvp.Key;
                var uses = kvp.Value;
                var failures = failureStats.ContainsKey(toolName) ? failureStats[toolName] : 0;
                var toolFailureRate = uses > 0 ? ((double)failures / uses * 100) : 0;
                var toolSuccessRate = 100 - toolFailureRate;

                sb.AppendLine($"  • {toolName}:");
                sb.AppendLine($"    - Uses: {uses} | Failures: {failures} | Failure Rate: {toolFailureRate:F1}% | Success: {toolSuccessRate:F1}%");

            }

            if (totalFailures > 0)
            {
                sb.AppendLine(" Most Problematic Tools:");
                var problematicTools = failureStats
                    .Where(f => f.Value > 0)
                    .OrderByDescending(f => f.Value)
                    .Take(5);

                foreach (var tool in problematicTools)
                {
                    var failureRate = stats.ContainsKey(tool.Key) ?
                        ((double)tool.Value / stats[tool.Key] * 100) : 100;
                    sb.AppendLine($"   {tool.Key}: {tool.Value} failures ({failureRate:F1}% failure rate)");
                }
                sb.AppendLine();
            }

            sb.AppendLine();
            sb.AppendLine("💡 Suggestions for useful but underused tools:");

            var leastUsed = stats.OrderBy(k => k.Value)
                                 .Where(k => k.Value < 3)
                                 .Take(3)
                                 .Select(k => k.Key);

            foreach (var tool in leastUsed)
            {
                sb.AppendLine($"👉 Try using „{tool}” more often. {Description}.");
            }

            return Task.FromResult(CommandResult.Ok(sb.ToString()));
        }
    }
}