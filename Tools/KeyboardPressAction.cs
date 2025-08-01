using WindowsInput;
using WindowsInput.Native;
using duo_code.Tools.Core;

namespace duo_code.Tools;

public class KeyboardPressAction : ToolActionBase
{
    public override string ToolName => "KEYBOARD_PRESS";

    public override string Description => @"Press key combinations or special keys.
KEYBOARD_PRESS: key1+key2+key3
Examples:
KEYBOARD_PRESS: Enter
KEYBOARD_PRESS: Ctrl+C
KEYBOARD_PRESS: Alt+Tab
KEYBOARD_PRESS: Ctrl+Shift+Esc
KEYBOARD_PRESS: Win+D";

    public string Keys { get; set; } = string.Empty;
    
    private static readonly InputSimulator _inputSimulator = new InputSimulator();

    protected override string ExecuteCore(string baseDirectory)
    {
        try
        {
            if (string.IsNullOrEmpty(Keys))
            {
                return "Error: No keys provided to press";
            }

            // Parse and send the key combination
            var result = ParseAndSendKeys(Keys);
            
            return result ? $"Pressed: {Keys}" : $"Error: Unable to parse key combination '{Keys}'";
        }
        catch (Exception ex)
        {
            return $"Error pressing keys: {ex.Message}";
        }
    }

    private bool ParseAndSendKeys(string keys)
    {
        try
        {
            var parts = keys.Split('+');
            
            // Handle single key press
            if (parts.Length == 1)
            {
                return SendSingleKey(parts[0].Trim());
            }
            
            // Handle key combinations
            var modifiers = new List<VirtualKeyCode>();
            string mainKey = parts[parts.Length - 1].Trim();
            
            // Parse modifiers
            for (int i = 0; i < parts.Length - 1; i++)
            {
                var modifier = parts[i].Trim().ToLower();
                switch (modifier)
                {
                    case "ctrl":
                    case "control":
                        modifiers.Add(VirtualKeyCode.CONTROL);
                        break;
                    case "alt":
                        modifiers.Add(VirtualKeyCode.MENU);
                        break;
                    case "shift":
                        modifiers.Add(VirtualKeyCode.SHIFT);
                        break;
                    case "win":
                    case "windows":
                        modifiers.Add(VirtualKeyCode.LWIN);
                        break;
                }
            }
            
            // Send the key combination
            if (modifiers.Count > 0)
            {
                _inputSimulator.Keyboard.ModifiedKeyStroke(modifiers, GetVirtualKeyCode(mainKey));
            }
            else
            {
                SendSingleKey(mainKey);
            }
            
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    private bool SendSingleKey(string key)
    {
        var vk = GetVirtualKeyCode(key);
        if (vk != VirtualKeyCode.NONAME)
        {
            _inputSimulator.Keyboard.KeyPress(vk);
            return true;
        }
        
        // If not a special key, type it as text
        if (key.Length == 1)
        {
            _inputSimulator.Keyboard.TextEntry(key);
            return true;
        }
        
        return false;
    }
    
    private VirtualKeyCode GetVirtualKeyCode(string key)
    {
        var upperKey = key.ToUpper();
        
        // Try to parse as enum
        if (Enum.TryParse<VirtualKeyCode>(upperKey, out var result))
        {
            return result;
        }
        
        // Handle common aliases
        return upperKey switch
        {
            "ENTER" => VirtualKeyCode.RETURN,
            "ESC" => VirtualKeyCode.ESCAPE,
            "PAGEUP" => VirtualKeyCode.PRIOR,
            "PAGEDOWN" => VirtualKeyCode.NEXT,
            "DEL" => VirtualKeyCode.DELETE,
            "CTRL" => VirtualKeyCode.CONTROL,
            "ALT" => VirtualKeyCode.MENU,
            "WIN" => VirtualKeyCode.LWIN,
            _ => VirtualKeyCode.NONAME
        };
    }

    public override string ToString()
    {
        return $"{ToolName}: {Keys}";
    }
}