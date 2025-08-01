using System.Runtime.InteropServices;
using duo_code.Tools.Core;

namespace duo_code.Tools;

public class MouseScrollAction : ToolActionBase
{
    public override string ToolName => "MOUSE_SCROLL";

    public override string Description => @"Move mouse to position and scroll the wheel up or down.
MOUSE_SCROLL: x,y,direction,amount
Examples:
MOUSE_SCROLL: 500,300,up,3
MOUSE_SCROLL: 800,600,down,5
MOUSE_SCROLL: 100,200,up,1";

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, int dwData, UIntPtr dwExtraInfo);

    private const uint MOUSEEVENTF_WHEEL = 0x0800;
    private const int WHEEL_DELTA = 120;

    public string Parameters { get; set; } = string.Empty;

    protected override string ExecuteCore(string baseDirectory)
    {
        try
        {
            var parts = Parameters.Split(',').Select(p => p.Trim()).ToArray();
            
            if (parts.Length != 4)
            {
                return "Error: Invalid format. Use 'x,y,direction,amount' (e.g., '500,300,up,3')";
            }

            // Parse coordinates
            if (!int.TryParse(parts[0], out int x) || !int.TryParse(parts[1], out int y))
            {
                return "Error: Invalid coordinates. Both x and y must be integers";
            }

            // Parse direction and amount
            var direction = parts[2].ToLower();
            if (!int.TryParse(parts[3], out int amount) || amount < 1 || amount > 10)
            {
                return "Error: Amount must be a number between 1 and 10";
            }

            int delta = direction switch
            {
                "up" => WHEEL_DELTA * amount,
                "down" => -WHEEL_DELTA * amount,
                _ => 0
            };

            if (delta == 0)
            {
                return "Error: Direction must be 'up' or 'down'";
            }

            // Move mouse to position first
            SetCursorPos(x, y);
            
            // Small delay to ensure position is set
            System.Threading.Thread.Sleep(50);

            // Perform the scroll
            mouse_event(MOUSEEVENTF_WHEEL, 0, 0, delta, UIntPtr.Zero);

            return $"Moved to ({x}, {y}) and scrolled {direction} {amount} notch{(amount > 1 ? "es" : "")}";
        }
        catch (Exception ex)
        {
            return $"Error scrolling: {ex.Message}";
        }
    }

    public override string ToString()
    {
        return $"{ToolName}: {Parameters}";
    }
}