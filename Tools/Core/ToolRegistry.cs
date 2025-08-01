using duo_code.Models;
using System.Text;

namespace duo_code.Tools.Core
{
    public class ToolRegistry
    {
        private readonly Dictionary<string, Type> _toolTypes = new();

        public ToolRegistry()
        {
            RegisterTools();
        }

        private void RegisterTools()
        {
            Register<ListFilesAction>();
            Register<FindAction>();
            Register<SearchAction>();
            Register<ReadFileAction>();
            Register<CreateFileAction>();
            Register<UpdateFileAction>();
            Register<DeleteFileAction>();
            Register<CreateDirectoryAction>();
            Register<DeleteDirectoryAction>();
            Register<RenameFileAction>();
            Register<RunCommandAction>();
            Register<EndTurnAction>();
            Register<NotesAction>();
            Register<SpawnSubagentAction>();
            Register<MouseClickAction>();
            Register<MouseMoveAction>();
            Register<MouseScrollAction>();
            Register<KeyboardTypeAction>();
            Register<KeyboardPressAction>();
            Register<WaitAction>();
        }

        private void Register<T>() where T : IToolAction, new()
        {
            var instance = new T();
            _toolTypes[instance.ToolName] = typeof(T);
        }

        public string GetAllToolInstructions(AgentMode mode = AgentMode.Default)
        {
            var sb = new StringBuilder();
            sb.AppendLine("You have access to the following tools:");
            sb.AppendLine();

            // Create temporary instances to get names and descriptions
            var toolInfos = new List<(string name, string description)>();
            foreach (var toolType in _toolTypes.Values)
            {
                var instance = Activator.CreateInstance(toolType) as IToolAction;
                if (instance != null && IsToolAvailableForMode(instance.ToolName, mode))
                {
                    toolInfos.Add((instance.ToolName, instance.Description));
                }
            }

            foreach (var tool in toolInfos.OrderBy(t => t.name))
            {
                sb.AppendLine($"**{tool.name}**: {tool.description}");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private bool IsToolAvailableForMode(string toolName, AgentMode mode)
        {
            return mode switch
            {
                AgentMode.Default => IsDefaultModeTool(toolName),
                AgentMode.Planning => IsPlanningModeTool(toolName),
                AgentMode.Orchestrator => IsOrchestratorModeTool(toolName),
                _ => true
            };
        }

        private bool IsDefaultModeTool(string toolName)
        {
            // Default mode has access to all core file and analysis tools
            var defaultTools = new HashSet<string>
            {
                "MOUSE_CLICK", "MOUSE_MOVE", "MOUSE_SCROLL", "KEYBOARD_TYPE", "KEYBOARD_PRESS", "WAIT"
            };
            return defaultTools.Contains(toolName);
        }

        private bool IsPlanningModeTool(string toolName)
        {
            // Planning mode has access to read-only tools and planning tools
            var planningTools = new HashSet<string>
            {
                "LIST_FILES", "FIND", "SEARCH", "READ_FILE", "FINISH_TASK", "NOTES"
            };
            return planningTools.Contains(toolName);
        }

        private bool IsOrchestratorModeTool(string toolName)
        {
            // Orchestrator mode can spawn subagents and use basic analysis tools
            var orchestratorTools = new HashSet<string>
            {
                "LIST_FILES", "FIND", "SEARCH", "READ_FILE", "SPAWN_SUBAGENT", "FINISH_TASK", "NOTES"
            };
            return orchestratorTools.Contains(toolName);
        }

        public List<string> GetToolNames()
        {
            return _toolTypes.Keys.ToList();
        }

        public IToolAction? CreateTool(string name, AgentMode mode = AgentMode.Default)
        {
            if (_toolTypes.TryGetValue(name, out var type) && IsToolAvailableForMode(name, mode))
            {
                return Activator.CreateInstance(type) as IToolAction;
            }
            return null;
        }
    }
}