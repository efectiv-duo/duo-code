using System;
using System.IO;
using System.Linq;
using System.Text;
using duo_code.Tools.Core;

namespace duo_code.Services
{
    public class ContextBuilder
    {
        private readonly ToolRegistry _toolRegistry;

        public ContextBuilder()
        {
            _toolRegistry = new ToolRegistry();
        }

        public string BuildContext()
        {
            var builder = new StringBuilder();

            // Load system prompt from markdown file
            var systemPromptPath = GetSystemPromptPath();
            if (File.Exists(systemPromptPath))
            {
                var systemPrompt = File.ReadAllText(systemPromptPath);
                // Remove the # System Prompt header line if present
                if (systemPrompt.StartsWith("# System Prompt"))
                {
                    systemPrompt = string.Join(Environment.NewLine,
                        systemPrompt.Split(new[] { '\r', '\n' }, StringSplitOptions.None)
                        .Skip(1)
                        .SkipWhile(string.IsNullOrWhiteSpace));
                }
                builder.AppendLine(systemPrompt);
            }
            else
            {
                // Fallback to minimal prompt if file not found
                builder.AppendLine("I am an intelligent coding assistant.");
                builder.AppendLine();
            }

            builder.AppendLine();
            builder.AppendLine($"Working directory: {Directory.GetCurrentDirectory()}");
            builder.AppendLine();

            // Add tool instructions
            builder.AppendLine("## Available Tools");
            builder.AppendLine(_toolRegistry.GetAllToolInstructions());

            return builder.ToString();
        }

        private string GetSystemPromptPath()
        {
            // Try multiple paths to find the system prompt file
            var possiblePaths = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), "Prompts", "system-prompt.md"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", "system-prompt.md"),
                Path.Combine(Directory.GetCurrentDirectory(), "system-prompt.md"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "system-prompt.md")
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                    return path;
            }

            // Return the first path as default (for error messaging)
            return possiblePaths[0];
        }
    }
}