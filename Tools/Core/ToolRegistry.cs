using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using duo_code.Models;
using duo_code.Tools;

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
            Register<EditFileAction>();
            Register<DeleteFileAction>();
            Register<CreateDirectoryAction>();
            Register<DeleteDirectoryAction>();
            Register<RenameFileAction>();
            Register<RunCommandAction>();
            Register<EndTurnAction>();
            Register<UpdateTodosAction>();
            Register<SpawnSubagentAction>();
        }

        private void Register<T>() where T : IToolAction, new()
        {
            var instance = new T();
            _toolTypes[instance.ToolName] = typeof(T);
        }

        public string GetAllToolInstructions(Mode mode = Mode.Default)
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

        private bool IsToolAvailableForMode(string toolName, Mode mode)
        {
            return mode switch
            {
                Mode.Default => IsDefaultModeTool(toolName),
                Mode.Planning => IsPlanningModeTool(toolName),
                Mode.Orchestrator => IsOrchestratorModeTool(toolName),
                _ => true
            };
        }

        private bool IsDefaultModeTool(string toolName)
        {
            // Default mode has access to all core file and analysis tools
            var defaultTools = new HashSet<string>
            {
                "LIST_FILES", "FIND", "SEARCH", "READ_FILE", "CREATE_FILE", "UPDATE_FILE",
                "DELETE_FILE", "CREATE_DIRECTORY", "DELETE_DIRECTORY", "RENAME_FILE", 
                "RUN_COMMAND", "FINISH_TASK", "UPDATE_TODOS"
            };
            return defaultTools.Contains(toolName);
        }

        private bool IsPlanningModeTool(string toolName)
        {
            // Planning mode has access to read-only tools and planning tools
            var planningTools = new HashSet<string>
            {
                "LIST_FILES", "FIND", "SEARCH", "READ_FILE", "FINISH_TASK", "UPDATE_TODOS"
            };
            return planningTools.Contains(toolName);
        }

        private bool IsOrchestratorModeTool(string toolName)
        {
            // Orchestrator mode can spawn subagents and use basic analysis tools
            var orchestratorTools = new HashSet<string>
            {
                "LIST_FILES", "FIND", "SEARCH", "READ_FILE", "SPAWN_SUBAGENT", "FINISH_TASK", "UPDATE_TODOS"
            };
            return orchestratorTools.Contains(toolName);
        }

        public List<string> GetToolNames()
        {
            return _toolTypes.Keys.ToList();
        }

        public IToolAction? CreateTool(string name, Mode mode = Mode.Default)
        {
            if (_toolTypes.TryGetValue(name, out var type) && IsToolAvailableForMode(name, mode))
            {
                return Activator.CreateInstance(type) as IToolAction;
            }
            return null;
        }
    }
}