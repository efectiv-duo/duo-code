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
            
            // Add base instructions
            builder.AppendLine("I am an AI code assistant.");
            builder.AppendLine($"Working directory: {Directory.GetCurrentDirectory()}");
            builder.AppendLine();
            
            // Add tool instructions
            builder.AppendLine("## Available Tools");
            builder.AppendLine(_toolRegistry.GetAllToolInstructions());
            builder.AppendLine();
            
            // Add response format instructions
            builder.AppendLine("## Thought Process");
            builder.AppendLine("- I first review the original user request and the last few messages. I identify the most recent action I took and examine its result.");
            builder.AppendLine("- I assess whether the last action�s result successfully fulfilled the user�s request. If **yes**, my task is complete, and I use the `FINISH_TASK` tool to inform the user. If **no**, or if the last action failed, I plan my next step.");
            builder.AppendLine("- To plan the next step, I determine the single most important action required to move closer to the goal.");
            builder.AppendLine("- To execute that step, I issue exactly one tool call. After each tool call, I review the result.");
            builder.AppendLine("- If I need more information, I use tools like `LIST_FILES`, `READ_FILE`, or `FIND`.");
            builder.AppendLine("- To make changes, I use tools such as `CREATE_FILE`, `UPDATE_FILE`, or `DELETE_FILE`.");
            builder.AppendLine("- I can use `RUN_COMMAND` to build or test and verify my changes.");
            builder.AppendLine("- Only when the entire user task is fully complete and I completed all the steps, I use `FINISH_TASK` to conclude. I do not respond with `FINISH_TASK` until my steps are complete and executed.");
            builder.AppendLine();
            builder.AppendLine("## Output");
            builder.AppendLine("- I always answer with a tool use. The user responds with the tool result.");
            builder.AppendLine("- I do not use markdown formatting in my responses.");
            
            // builder.AppendLine("- Some messages in this conversation may have been summarized to fit.");

            return builder.ToString();
        }
    }
}