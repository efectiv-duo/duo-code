using System.Text;

namespace duo_code.Models
{
    public enum Mode
    {
        Default,
        Planning,
        Orchestrator
    }

    public static class ModeExtensions
    {
        public static string ToDisplayString(this Mode mode)
        {
            return mode switch
            {
                Mode.Default => "DEFAULT",
                Mode.Planning => "PLANNING",
                Mode.Orchestrator => "ORCHESTRATOR",
                _ => "UNKNOWN"
            };
        }

        public static string GetSystemPrompt(this Mode mode)
        {
            return mode switch
            {
                Mode.Default => GetDefaultSystemPrompt(),
                Mode.Planning => GetPlanningSystemPrompt(),
                Mode.Orchestrator => GetOrchestratorSystemPrompt(),
                _ => GetDefaultSystemPrompt()
            };
        }

        private static string GetDefaultSystemPrompt()
        {
            var builder = new StringBuilder();

            builder.AppendLine(@"You are an agent.");
            builder.AppendLine(@"Use the FINISH_TASK tool when you have completed all your tasks.");
            builder.AppendLine();
            builder.AppendLine("## Core Loop");
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
            builder.AppendLine("- I always answer with tool uses. The user responds with the tools result.");
            builder.AppendLine("- I will run multiple tools in one turn, but only if they don't depend on each other's output.");
            builder.AppendLine("- I do not use markdown formatting in my responses.");
            builder.AppendLine("- Path should always start from current directory (.)");
            builder.AppendLine("- Tools RUN");

            return builder.ToString();
        }

        private static string GetPlanningSystemPrompt()
        {
            var builder = new StringBuilder();

            builder.AppendLine(@"You are a planning assistant.");
            builder.AppendLine(@"Create detailed plans for user requests but do not execute them.");
            builder.AppendLine(@"Always use the FINISH_TASK tool when you have completed your planning.");

            return builder.ToString();
        }

        private static string GetOrchestratorSystemPrompt()
        {
            var builder = new StringBuilder();

            builder.AppendLine(@"You are an orchestrator that follows a workflow loop.");
            builder.AppendLine(@"You can either respond directly or spawn subagents to execute specific tasks.");
            builder.AppendLine(@"You have access to the SPAWN_SUBAGENT tool to create subagents that will execute tasks and return results to you.");
            builder.AppendLine(@"Always use FINISH_TASK when the entire orchestrated task is complete.");
            builder.AppendLine();
            builder.AppendLine("# OODA Orchestration Workflow");
            builder.AppendLine();
            builder.AppendLine("## Core Loop");
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
            builder.AppendLine("## Orchestrator Rules");
            builder.AppendLine("- Spawn subagents for focused tasks at each step");
            builder.AppendLine("- Synthesize subagent results before proceeding");
            builder.AppendLine("- Adapt workflow based on task complexity");
            builder.AppendLine("- Skip/combine steps for simple requests");

            return builder.ToString();
        }
    }
}