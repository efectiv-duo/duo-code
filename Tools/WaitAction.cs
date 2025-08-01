using duo_code.Tools.Core;

namespace duo_code.Tools;

public class WaitAction : ToolActionBase
{
    public override string ToolName => "WAIT";

    public override string Description => @"Wait for a specified number of seconds. Useful when waiting for an app to open or for something to finish executing.
WAIT: seconds
Examples:
WAIT: 1
WAIT: 2.5
WAIT: 0.5";

    public string Seconds { get; set; } = string.Empty;

    protected override string ExecuteCore(string baseDirectory)
    {
        try
        {
            if (string.IsNullOrEmpty(Seconds))
            {
                return "Error: No duration provided";
            }

            if (!double.TryParse(Seconds, out double seconds) || seconds < 0 || seconds > 30)
            {
                return "Error: Duration must be a number between 0 and 30 seconds";
            }

            // Convert to milliseconds
            int milliseconds = (int)(seconds * 1000);
            
            // Perform the wait
            System.Threading.Thread.Sleep(milliseconds);

            return $"Waited {seconds} second{(seconds == 1 ? "" : "s")}";
        }
        catch (Exception ex)
        {
            return $"Error waiting: {ex.Message}";
        }
    }

    public override string ToString()
    {
        return $"{ToolName}: {Seconds}";
    }
}