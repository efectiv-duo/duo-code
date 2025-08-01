using System.Runtime.InteropServices;
using duo_code.Tools.Core;

namespace duo_code.Tools;

public class MouseClickAction : ToolActionBase
{
    public override string ToolName => "MOUSE_CLICK";

    public override string Description => @"Click the mouse at the current position or specified coordinates. To click something, make sure you position the cursor on the center of the item you want to click.
MOUSE_CLICK: x,y
MOUSE_CLICK: left
MOUSE_CLICK: right
MOUSE_CLICK: middle
MOUSE_CLICK: 100,200,left";

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;

    public string Input { get; set; } = string.Empty;

    protected override string ExecuteCore(string baseDirectory)
    {
        try
        {
            var parts = Input.Split(',').Select(p => p.Trim()).ToArray();
            
            if (parts.Length == 1)
            {
                // Just button type - click at current position
                PerformClick(parts[0].ToLower());
                return $"Clicked {parts[0]} button at current position";
            }
            else if (parts.Length == 2)
            {
                // x,y coordinates - left click
                if (int.TryParse(parts[0], out int x) && int.TryParse(parts[1], out int y))
                {
                    SetCursorPos(x, y);
                    System.Threading.Thread.Sleep(50); // Small delay to ensure position is set
                    PerformClick("left");
                    return $"Left clicked at ({x}, {y})";
                }
                else
                {
                    return "Error: Invalid coordinates format";
                }
            }
            else if (parts.Length == 3)
            {
                // x,y,button
                if (int.TryParse(parts[0], out int x) && int.TryParse(parts[1], out int y))
                {
                    SetCursorPos(x, y);
                    System.Threading.Thread.Sleep(50); // Small delay to ensure position is set
                    PerformClick(parts[2].ToLower());
                    return $"Clicked {parts[2]} button at ({x}, {y})";
                }
                else
                {
                    return "Error: Invalid coordinates format";
                }
            }
            else
            {
                return "Error: Invalid input format. Use 'button', 'x,y', or 'x,y,button'";
            }
        }
        catch (Exception ex)
        {
            return $"Error performing mouse click: {ex.Message}";
        }
    }

    private void PerformClick(string button)
    {
        switch (button)
        {
            case "left":
                mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
                break;
            case "right":
                mouse_event(MOUSEEVENTF_RIGHTDOWN | MOUSEEVENTF_RIGHTUP, 0, 0, 0, UIntPtr.Zero);
                break;
            case "middle":
                mouse_event(MOUSEEVENTF_MIDDLEDOWN | MOUSEEVENTF_MIDDLEUP, 0, 0, 0, UIntPtr.Zero);
                break;
            default:
                throw new ArgumentException($"Unknown button type: {button}");
        }
    }

    public override string ToString()
    {
        return $"{ToolName}: {Input}";
    }
}