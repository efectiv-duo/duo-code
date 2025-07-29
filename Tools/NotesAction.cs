using duo_code.Tools.Core;

namespace duo_code.Tools;

public class NotesAction : ToolActionBase
{
    // Static todo list that persists across instances
    public static string CurrentNotes { get; private set; } = string.Empty;
    
    public override string ToolName => "NOTES";
    public override string Description => @"My own notepad with plain text format.
Format:
NOTES:
add_notes";
    
    public string Notes { get; set; } = string.Empty;
    
    protected override string ExecuteCore(string baseDirectory)
    {
        // Update the static notes
        CurrentNotes += Notes;
        
        return $"Updated NOTES:\n{Notes}";
    }
    
    public override string ToString()
    {
        return $"{ToolName}:\n{Notes}";
    }
}