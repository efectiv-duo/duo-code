using duo_code.Tools.Core;

namespace duo_code.Tools;

public class UpdateTodosAction : ToolActionBase
{
    // Static todo list that persists across instances
    public static string CurrentTodoList { get; private set; } = string.Empty;
    
    public override string ToolName => "UPDATE_TODOS";
    public override string Description => @"Update the todo list with plain text format using checkboxes.
Format:
UPDATE_TODOS:
[x] Task 1 (completed)
[x] Task 2 (skipped)
[ ] Task 3";
    
    public string TodoList { get; set; } = string.Empty;
    
    protected override string ExecuteCore(string baseDirectory)
    {
        // Update the static todo list
        CurrentTodoList = TodoList;

        if (string.IsNullOrWhiteSpace(TodoList))
        {
            return "Todo list cleared.";
        }
        
        return $"Todo list updated:\n{TodoList}";
    }
    
    public override string ToString()
    {
        return $"{ToolName}:\n{TodoList}";
    }
}