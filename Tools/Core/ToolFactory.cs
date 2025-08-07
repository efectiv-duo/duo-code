using duo_code.Services;

namespace duo_code.Tools.Core;

public static class ToolFactory
{
    private static CompressCodeService? _compressService;

    public static void SetCompressService(CompressCodeService service)
    {
        _compressService = service;
    }

    // Updated to return a list of IToolAction
    public static List<IToolAction> Parse(string response)
    {
        var actions = new List<IToolAction>();
        var lines = response.Replace("\r\n", "\n").Split('\n');

        if (lines.Length == 0) return actions;

        int i = 0;
        while (i < lines.Length)
        {
            // Skip empty lines
            while (i < lines.Length && string.IsNullOrWhiteSpace(lines[i]))
            {
                i++;
            }

            if (i >= lines.Length) break;

            // Check if current line is a tool command
            var currentLine = lines[i].Trim();
            if (!IsToolCommand(currentLine))
            {
                i++;
                continue;
            }

            // Parse tool command
            var parts = currentLine.Split(new[] { ':' }, 2);
            string toolType = parts[0].Trim();
            string args = parts.Length > 1 ? parts[1].Trim() : string.Empty;

            // Find the content for this tool (if needed)
            int contentStartIndex = i + 1;
            int contentEndIndex = contentStartIndex;

            // For tools that need content, find where the next tool starts
            if (toolType == "CREATE_FILE" || toolType == "UPDATE_FILE" || toolType == "NOTES" || toolType == "SPAWN_SUBAGENT")
            {
                // Find the next tool command or end of input
                while (contentEndIndex < lines.Length)
                {
                    if (contentEndIndex > contentStartIndex && IsToolCommand(lines[contentEndIndex].Trim()))
                    {
                        break;
                    }
                    contentEndIndex++;
                }
            }

            // Create the tool action
            IToolAction? action = null;
            switch (toolType)
            {
                case "CREATE_FILE":
                    var createContent = string.Join(Environment.NewLine, lines.Skip(contentStartIndex).Take(contentEndIndex - contentStartIndex));
                    action = new CreateFileAction { Path = args, Content = createContent, ConsoleRequestMessage = currentLine };
                    break;
                case "UPDATE_FILE":
                    var updateContent = string.Join(Environment.NewLine, lines.Skip(contentStartIndex).Take(contentEndIndex - contentStartIndex));
                    action = new UpdateFileAction { Path = args, Content = updateContent, ConsoleRequestMessage = currentLine };
                    break;
                case "DELETE_FILE":
                    action = new DeleteFileAction { Path = args, ConsoleRequestMessage = currentLine };
                    break;
                case "CREATE_DIR":
                    action = new CreateDirectoryAction { Path = args, ConsoleRequestMessage = currentLine };
                    break;
                case "DELETE_DIR":
                    action = new DeleteDirectoryAction { Path = args, ConsoleRequestMessage = currentLine };
                    break;
                case "LIST_FILES":
                    action = ParseListFilesAction(args, currentLine);
                    break;
                case "FIND":
                    action = new FindAction { Pattern = args, ConsoleRequestMessage = currentLine };
                    break;
                case "SEARCH":
                    action = new SearchAction { Pattern = args, ConsoleRequestMessage = currentLine };
                    break;
                case "READ_FILE":
                    action = ParseReadFileAction(args, currentLine);
                    break;
                case "RUN_COMMAND":
                    action = new RunCommandAction { Command = args, ConsoleRequestMessage = currentLine };
                    break;
                case "RENAME_FILE":
                    var paths = args.Split(new[] { '>' }, 2);
                    if (paths.Length != 2) throw new ArgumentException("Invalid RENAME_FILE format. Use 'old_path > new_path'");
                    action = new RenameFileAction { OldPath = paths[0].Trim(), NewPath = paths[1].Trim(), ConsoleRequestMessage = currentLine };
                    break;
                case "NOTES":
                    var notes = string.Join(Environment.NewLine, lines.Skip(contentStartIndex).Take(contentEndIndex - contentStartIndex));
                    action = new NotesAction { Notes = notes };
                    break;
                case "SPAWN_SUBAGENT":
                    var prompt = string.Join(Environment.NewLine, lines.Skip(contentStartIndex).Take(contentEndIndex - contentStartIndex));
                    action = new SpawnSubagentAction { TaskDescription = args, Prompt = prompt, ConsoleRequestMessage = currentLine };
                    break;
            }

            if (action != null)
            {
                ToolAnalytics.LogToolUsage(toolType);
                actions.Add(action);
            }

            // Move to the next potential tool
            i = toolType == "CREATE_FILE" || toolType == "UPDATE_FILE" || toolType == "NOTES" || toolType == "SPAWN_SUBAGENT"
                ? contentEndIndex
                : i + 1;
        }

        return actions;
    }

    private static bool IsToolCommand(string line)
    {
        line = line.Trim();
        var registry = new ToolRegistry();
        var validTools = registry.GetToolNames();

        // Check if the line starts with "TOOL_NAME:"
        return validTools.Any(tool => line.StartsWith(tool + ":"));
    }

    private static ListFilesAction ParseListFilesAction(string args, string currentLine)
    {
        if (string.IsNullOrEmpty(args))
            return new ListFilesAction { Path = ".", ConsoleRequestMessage = currentLine };

        var parts = args.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        
        if (parts.Length == 0)
            return new ListFilesAction { Path = ".", ConsoleRequestMessage = currentLine };

        var action = new ListFilesAction { Path = parts[0], ConsoleRequestMessage = currentLine };

        // Parse additional parameters
        for (int i = 1; i < parts.Length; i++)
        {
            if (parts[i].StartsWith("depth:"))
            {
                var depthStr = parts[i].Substring("depth:".Length);
                if (int.TryParse(depthStr, out int depth) && depth >= 1 && depth <= 5)
                {
                    action.Depth = depth;
                }
            }
            else if (parts[i].StartsWith("directories_only:"))
            {
                var dirOnlyStr = parts[i].Substring("directories_only:".Length).ToLowerInvariant();
                action.DirectoriesOnly = dirOnlyStr == "true" || dirOnlyStr == "1" || dirOnlyStr == "yes";
            }
        }

        return action;
    }

    private static ReadFileAction ParseReadFileAction(string args, string currentLine)
    {
        if (string.IsNullOrEmpty(args))
            return new ReadFileAction { Path = "", CompressService = _compressService, ConsoleRequestMessage = currentLine };

        // Parse path and optional parameters (lines and purpose)
        var parts = args.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
            return new ReadFileAction { Path = "", CompressService = _compressService, ConsoleRequestMessage = currentLine };

        var action = new ReadFileAction { Path = parts[0], CompressService = _compressService, ConsoleRequestMessage = currentLine };

        // Parse additional parameters
        for (int i = 1; i < parts.Length; i++)
        {
            if (parts[i].StartsWith("lines:"))
            {
                action.LineRange = parts[i].Substring("lines:".Length);
            }
            else if (parts[i].StartsWith("purpose:"))
            {
                // Purpose might contain spaces, so we need to collect all remaining parts
                var purposeParts = new List<string> { parts[i].Substring("purpose:".Length) };

                // Collect remaining parts that don't start with a known parameter
                for (int j = i + 1; j < parts.Length; j++)
                {
                    if (!parts[j].StartsWith("lines:") && !parts[j].StartsWith("purpose:"))
                    {
                        purposeParts.Add(parts[j]);
                        i = j; // Update i to skip processed parts
                    }
                    else
                    {
                        break;
                    }
                }

                action.Purpose = string.Join(" ", purposeParts);
            }
        }

        return action;
    }
}