using WindowsInput;
using duo_code.Tools.Core;

namespace duo_code.Tools;

public class KeyboardTypeAction : ToolActionBase
{
    public override string ToolName => "KEYBOARD_TYPE";

    public override string Description => @"Type text at the current cursor position.
KEYBOARD_TYPE: text to type";

    public string Text { get; set; } = string.Empty;
    
    private static readonly InputSimulator _inputSimulator = new InputSimulator();

    protected override string ExecuteCore(string baseDirectory)
    {
        try
        {
            if (string.IsNullOrEmpty(Text))
            {
                return "Error: No text provided to type";
            }

            // Use InputSimulator for fast text entry
            _inputSimulator.Keyboard.TextEntry(Text);
            
            return $"Typed: {Text}";
        }
        catch (Exception ex)
        {
            return $"Error typing text: {ex.Message}";
        }
    }

    public override string ToString()
    {
        return $"{ToolName}: {Text}";
    }
}