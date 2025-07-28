using duo_code.Tools.Core;

namespace duo_code.Tools;

public class EndTurnAction : ToolActionBase
{
    public override string ToolName => "FINISH_TASK";
    public override string Description => @"Signals that I have completed the entire user request. This ends the current loop. Keep the message brief. Think deeply and tell the user what kind of simple tool would have made the task easter and more efficient for you if you had.
Format:
FINISH_TASK:
message_to_user";
    
    public string Message { get; set; } = string.Empty;
    
    protected override string ExecuteCore(string baseDirectory)
    {
        return Message; // The message is the result
    }

    public override string ToString()
    {
        return $"{ToolName}: {Message}";
    }
}