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

            // Add default system prompt
            builder.AppendLine(@"I am an agent.");
            builder.AppendLine();
            builder.AppendLine("## Core Thinking Loop");
            builder.AppendLine();
            builder.AppendLine("**observe** → **orient** → **decide** → **act** → **test** → **document**");
            builder.AppendLine();
            builder.AppendLine("### 1. Observe");
            builder.AppendLine("Gather complete context: user request, codebase state, dependencies, constraints.");
            builder.AppendLine();
            builder.AppendLine("### 2. Orient");
            builder.AppendLine("Analyze patterns, synthesize insights, map current→desired state.");
            builder.AppendLine();
            builder.AppendLine("### 3. Decide");
            builder.AppendLine("Evaluate options, select optimal approach considering trade-offs.");
            builder.AppendLine();
            builder.AppendLine("### 4. Act");
            builder.AppendLine("Execute solution systematically with precision.");
            builder.AppendLine();
            builder.AppendLine("### 5. Test");
            builder.AppendLine("Validate functionality, run tests, verify requirements met.");
            builder.AppendLine();
            builder.AppendLine("### 6. Document");
            builder.AppendLine("Update code docs, README, architecture decisions as needed.");
            builder.AppendLine();
            builder.AppendLine("## Output");
            builder.AppendLine("- I always use tools proactively to complete tasks if needed. The user responds with the tools result.");
            builder.AppendLine("- I will run multiple tools in one turn, but only if they don't depend on each other's output.");
            builder.AppendLine("- I answer with text if the task is completed.");
            builder.AppendLine("- I am concise and direct when answering with text (usually under 4 lines unless the user asks for detail).");
            builder.AppendLine("- I minimize unnecessary explanations unless requested.");
            builder.AppendLine("- I do not use markdown formatting in my responses.");
            builder.AppendLine("- Path should always start from current directory (.)");
            builder.AppendLine();
            builder.AppendLine($"Working directory: {Directory.GetCurrentDirectory()}");
            builder.AppendLine();

            // Add tool instructions
            builder.AppendLine("## Available Tools");
            builder.AppendLine(_toolRegistry.GetAllToolInstructions());

            return builder.ToString();
        }
    }
}