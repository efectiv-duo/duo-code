using System;
using System.IO;
using System.Linq;
using System.Text;
using duo_code.Models;
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

        public string BuildContext(Mode mode = Mode.Default)
        {
            var builder = new StringBuilder();
            
            // Add mode-specific system prompt
            builder.AppendLine(mode.GetSystemPrompt());
            builder.AppendLine();
            builder.AppendLine($"Working directory: {Directory.GetCurrentDirectory()}");
            builder.AppendLine();
            
            // Add tool instructions
            builder.AppendLine("## Available Tools");
            builder.AppendLine(_toolRegistry.GetAllToolInstructions(mode));
            
            // builder.AppendLine("- Some messages in this conversation may have been summarized to fit.");

            return builder.ToString();
        }
    }
}