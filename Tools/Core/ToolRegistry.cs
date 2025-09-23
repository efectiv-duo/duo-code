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
            // Backend tools
            Register<CreateEntityAction>();
            Register<EntityConfigAction>();
            Register<QueryFeatureAction>();
            Register<CommandFeatureAction>();
            Register<ControllerEndpointAction>();
            Register<AddMigrationAction>();
            Register<SeedAction>();
            Register<DomainEventAction>();
            Register<EventHandlerAction>();
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
                if (instance != null && IncludeTool(instance.ToolName))
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

        private bool IncludeTool(string toolName)
        {
            // Include the tools that you want the agent to have access to
            var defaultTools = new HashSet<string>
            {
                "LIST_FILES", "FIND", "SEARCH", "READ_FILE",
                "ENTITY", "ENTITY_CONFIG", "QUERY_FEATURE", "COMMAND_FEATURE",
                "CONTROLLER_ENDPOINT", "ADD_MIGRATION", "SEED", "DOMAIN_EVENT", "EVENT_HANDLER"
            };
            return defaultTools.Contains(toolName);
        }

        public List<string> GetToolNames()
        {
            return _toolTypes.Keys.ToList();
        }
    }
}


// "GIT_SUMMARY", "CREATE_FILE", "UPDATE_FILE",
//"DELETE_FILE", "CREATE_DIR", "DELETE_DIR", "RENAME_FILE",
//                "RUN_COMMAND"