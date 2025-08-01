using System.Runtime.InteropServices;
using duo_code.Tools.Core;

namespace duo_code.Tools;

public class MouseMoveAction : ToolActionBase
{
    public override string ToolName => "MOUSE_MOVE";

    public override string Description => @"Move the mouse cursor to specified coordinates.
MOUSE_MOVE: x,y";

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    public string Coordinates { get; set; } = string.Empty;

    protected override string ExecuteCore(string baseDirectory)
    {
        try
        {
            var parts = Coordinates.Split(',').Select(p => p.Trim()).ToArray();
            
            if (parts.Length != 2)
            {
                return "Error: Invalid input format. Use 'x,y' coordinates";
            }

            if (!int.TryParse(parts[0], out int x) || !int.TryParse(parts[1], out int y))
            {
                return "Error: Invalid coordinates. Both x and y must be integers";
            }

            // Get current position for logging
            GetCursorPos(out POINT currentPos);
            
            // Move cursor
            bool success = SetCursorPos(x, y);
            
            if (success)
            {
                return $"Moved cursor from ({currentPos.X}, {currentPos.Y}) to ({x}, {y})";
            }
            else
            {
                return "Error: Failed to move cursor";
            }
        }
        catch (Exception ex)
        {
            return $"Error moving mouse: {ex.Message}";
        }
    }

    public override string ToString()
    {
        return $"{ToolName}: {Coordinates}";
    }
}