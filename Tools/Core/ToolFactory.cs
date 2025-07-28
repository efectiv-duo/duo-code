using duo_code.Services;
using duo_code.Tools;

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
            if (toolType == "CREATE_FILE" || toolType == "UPDATE_FILE" || toolType == "FINISH_TASK" || toolType == "UPDATE_TODOS")
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
                case "UPDATE_FILE":
                    var content = string.Join(Environment.NewLine, lines.Skip(contentStartIndex).Take(contentEndIndex - contentStartIndex));
                    action = toolType == "CREATE_FILE"
                        ? new CreateFileAction { Path = args, Content = content }
                        : new UpdateFileAction { Path = args, Content = content };
                    break;

                case "PATCH_FILE":
                    action = ParsePatchFileAction(args, lines, contentStartIndex, contentEndIndex);
                    break;

                case "DELETE_FILE":
                    action = new DeleteFileAction { Path = args };
                    break;
                case "CREATE_DIR":
                    action = new CreateDirectoryAction { Path = args };
                    break;
                case "DELETE_DIR":
                    action = new DeleteDirectoryAction { Path = args };
                    break;
                case "LIST_FILES":
                    action = ParseListFilesAction(args);
                    break;
                case "FIND":
                    action = new FindAction { Pattern = args };
                    break;
                case "READ_FILE":
                    action = new ReadFileAction { Path = args, CompressService = _compressService };
                    break;
                case "RUN_COMMAND":
                    action = new RunCommandAction { Command = args };
                    break;
                case "FINISH_TASK":
                    var message = string.Join(Environment.NewLine, lines.Skip(contentStartIndex).Take(contentEndIndex - contentStartIndex));
                    action = new EndTurnAction { Message = message };
                    break;
                case "RENAME_FILE":
                    var paths = args.Split(new[] { '>' }, 2);
                    if (paths.Length != 2) throw new ArgumentException("Invalid RENAME_FILE format. Use 'old_path > new_path'");
                    action = new RenameFileAction { OldPath = paths[0].Trim(), NewPath = paths[1].Trim() };
                    break;
                case "UPDATE_TODOS":
                    var todoList = string.Join(Environment.NewLine, lines.Skip(contentStartIndex).Take(contentEndIndex - contentStartIndex));
                    action = new UpdateTodosAction { TodoList = todoList };
                    break;
            }

            if (action != null)
            {
                actions.Add(action);
            }

            // Move to the next potential tool
            i = toolType == "CREATE_FILE" || toolType == "UPDATE_FILE" || toolType == "FINISH_TASK" || toolType == "UPDATE_TODOS" || toolType == "PATCH_FILE"
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

    private static ListFilesAction ParseListFilesAction(string args)
    {
        if (string.IsNullOrEmpty(args))
            return new ListFilesAction { Path = "." };

        // Check if args contains depth parameter
        var parts = args.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 1)
        {
            // Just a path, no depth
            return new ListFilesAction { Path = parts[0] };
        }
        else if (parts.Length == 2 && parts[1].StartsWith("depth:"))
        {
            // Path and depth
            var depthStr = parts[1].Substring("depth:".Length);
            if (int.TryParse(depthStr, out int depth) && depth >= 1 && depth <= 5)
            {
                return new ListFilesAction { Path = parts[0], Depth = depth };
            }
        }

        // Default behavior if parsing fails
        return new ListFilesAction { Path = args };
    }

    private static PatchFileAction ParsePatchFileAction(string filePath, string[] lines, int contentStartIndex, int contentEndIndex)
    {
        var patchAction = new PatchFileAction { Path = filePath };

        // Parse the new format with <<<< FIND / >>>> and <<<<< REPLACE / >>>>> blocks
        var i = contentStartIndex;
        while (i < contentEndIndex)
        {
            var line = lines[i].Trim();
            
            // Look for FIND block markers
            if (line.StartsWith("<<<<") || line.StartsWith("- ") || line.StartsWith("-"))
            {
                var patch = ParseSinglePatch(lines, ref i, contentEndIndex);
                if (patch != null)
                {
                    patchAction.Patches.Add(patch);
                }
            }
            else
            {
                i++;
            }
        }

        return patchAction;
    }

    private static SmartPatchOperation? ParseSinglePatch(string[] lines, ref int index, int contentEndIndex)
    {
        var currentLine = lines[index].Trim();
        
        // Handle legacy format (- and +)
        if (currentLine.StartsWith("- ") || currentLine.StartsWith("-"))
        {
            return ParseLegacyPatch(lines, ref index, contentEndIndex);
        }
        
        // Handle new format with <<<< blocks
        if (currentLine.StartsWith("<<<<"))
        {
            return ParseNewFormatPatch(lines, ref index, contentEndIndex);
        }
        
        index++;
        return null;
    }

    private static SmartPatchOperation? ParseLegacyPatch(string[] lines, ref int index, int contentEndIndex)
    {
        var findContent = new List<string>();
        var replaceContent = new List<string>();
        var inRemoveSection = true;
        
        // Parse remove section
        while (index < contentEndIndex && (lines[index].StartsWith("- ") || lines[index].StartsWith("-")))
        {
            var content = lines[index].StartsWith("- ") ? lines[index].Substring(2) : lines[index].Substring(1);
            findContent.Add(content);
            index++;
        }
        
        // Parse add section
        while (index < contentEndIndex && (lines[index].StartsWith("+ ") || lines[index].StartsWith("+")))
        {
            var content = lines[index].StartsWith("+ ") ? lines[index].Substring(2) : lines[index].Substring(1);
            replaceContent.Add(content);
            index++;
        }
        
        if (findContent.Count == 0)
            return null;
            
        return new SmartPatchOperation
        {
            MatchType = PatchMatchType.Exact,
            FindContent = string.Join(Environment.NewLine, findContent),
            ReplaceContent = string.Join(Environment.NewLine, replaceContent)
        };
    }

    private static SmartPatchOperation? ParseNewFormatPatch(string[] lines, ref int index, int contentEndIndex)
    {
        var currentLine = lines[index].Trim();
        
        // Determine match type from the FIND marker
        PatchMatchType matchType = PatchMatchType.Exact;
        if (currentLine.Contains("FIND_FUZZY"))
            matchType = PatchMatchType.Fuzzy;
        else if (currentLine.Contains("FIND_REGEX"))
            matchType = PatchMatchType.Regex;
        
        index++; // Move past the <<<< FIND line
        
        // Collect FIND content until we hit >>>>
        var findContent = new List<string>();
        while (index < contentEndIndex && !lines[index].Trim().StartsWith(">>>>"))
        {
            findContent.Add(lines[index]);
            index++;
        }
        
        if (index >= contentEndIndex || !lines[index].Trim().StartsWith(">>>>"))
        {
            // Malformed - missing closing >>>>
            throw new ArgumentException("Malformed PATCH_FILE: Missing >>>> to close FIND block");
        }
        
        index++; // Move past the >>>> line
        
        // Look for <<<<< REPLACE
        while (index < contentEndIndex && !lines[index].Trim().StartsWith("<<<<<"))
        {
            index++;
        }
        
        if (index >= contentEndIndex || !lines[index].Trim().Contains("REPLACE"))
        {
            // Malformed - missing REPLACE block
            throw new ArgumentException("Malformed PATCH_FILE: Missing <<<<< REPLACE block");
        }
        
        index++; // Move past the <<<<< REPLACE line
        
        // Collect REPLACE content until we hit >>>>>
        var replaceContent = new List<string>();
        while (index < contentEndIndex && !lines[index].Trim().StartsWith(">>>>>"))
        {
            replaceContent.Add(lines[index]);
            index++;
        }
        
        if (index >= contentEndIndex || !lines[index].Trim().StartsWith(">>>>>"))
        {
            // Malformed - missing closing >>>>>
            throw new ArgumentException("Malformed PATCH_FILE: Missing >>>>> to close REPLACE block");
        }
        
        index++; // Move past the >>>>> line
        
        return new SmartPatchOperation
        {
            MatchType = matchType,
            FindContent = string.Join(Environment.NewLine, findContent),
            ReplaceContent = string.Join(Environment.NewLine, replaceContent)
        };
    }
}