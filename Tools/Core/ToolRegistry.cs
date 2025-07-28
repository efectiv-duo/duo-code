using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            Register<ReadFileAction>();
            Register<CreateFileAction>();
            Register<UpdateFileAction>();
            Register<DeleteFileAction>();
            Register<CreateDirectoryAction>();
            Register<DeleteDirectoryAction>();
            Register<RenameFileAction>();
            Register<PatchFileAction>();
            Register<RunCommandAction>();
            Register<EndTurnAction>();
            Register<UpdateTodosAction>();
        }

        private void Register<T>() where T : IToolAction, new()
        {
            var instance = new T();
            _toolTypes[instance.ToolName] = typeof(T);
        }

        public string GetAllToolInstructions()
        {
            var sb = new StringBuilder();
            sb.AppendLine("You have access to the following tools:");
            sb.AppendLine();

            // Create temporary instances to get names and descriptions
            var toolInfos = new List<(string name, string description)>();
            foreach (var toolType in _toolTypes.Values)
            {
                var instance = Activator.CreateInstance(toolType) as IToolAction;
                if (instance != null)
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

        public List<string> GetToolNames()
        {
            return _toolTypes.Keys.ToList();
        }

        public IToolAction? CreateTool(string name)
        {
            if (_toolTypes.TryGetValue(name, out var type))
            {
                return Activator.CreateInstance(type) as IToolAction;
            }
            return null;
        }
    }
}