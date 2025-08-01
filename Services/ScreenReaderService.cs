using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace duo_code.Services;

/// <summary>
/// Provides functionality to capture the current visible screen elements as an XML document.
/// </summary>
public static class ScreenReaderService
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    /// <summary>
    /// Captures the foreground window and taskbar elements, returning a structured XML document.
    /// </summary>
    /// <returns>An XDocument representing the screen DOM.</returns>
    public static XDocument ReadScreen()
    {
        try
        {
            using var automation = new UIA3Automation();
            var desktop = automation.GetDesktop();

            var roots = new List<ScreenElement>();

            // Capture the active window
            try
            {
                var fgHandle = GetForegroundWindow();
                if (fgHandle != IntPtr.Zero)
                {
                    var fgElement = automation.FromHandle(fgHandle) as AutomationElement;
                    if (fgElement != null)
                    {
                        var node = BuildElement(fgElement);
                        if (node != null) roots.Add(node);
                    }
                }
            }
            catch
            {
                // Continue even if we can't get the foreground window
            }

            // Find the taskbar by class name
            try
            {
                var taskbar = desktop.FindFirst(TreeScope.Children,
                    automation.ConditionFactory.ByClassName("Shell_TrayWnd"));
                if (taskbar != null)
                {
                    var node = BuildElement(taskbar);
                    if (node != null) roots.Add(node);
                }
            }
            catch
            {
                // Continue even if we can't get the taskbar
            }

            // Assemble XML
            var rootXml = new XElement("Screen");
            foreach (var el in roots)
            {
                try
                {
                    rootXml.Add(BuildXmlElement(el));
                }
                catch
                {
                    // Skip elements that can't be converted to XML
                }
            }
            return new XDocument(rootXml);
        }
        catch
        {
            // Return empty screen document on any failure
            return new XDocument(new XElement("Screen"));
        }
    }

    private static XElement BuildXmlElement(ScreenElement el)
    {
        var xml = new XElement(el.ControlType,
            new XAttribute("center-x", (int)(el.Rect.X + el.Rect.W / 2)),
            new XAttribute("center-y", (int)(el.Rect.Y + el.Rect.H / 2)),
            new XAttribute("w", el.Rect.W),
            new XAttribute("h", el.Rect.H)
        );
        
        // Add optional attributes only if they have values
        if (el.IsEnabled.HasValue)
            xml.Add(new XAttribute("enabled", el.IsEnabled.Value.ToString().ToLower()));
            
        if (el.HasKeyboardFocus.HasValue)
            xml.Add(new XAttribute("focus", el.HasKeyboardFocus.Value.ToString().ToLower()));
            
        if (!string.IsNullOrEmpty(el.ToolTip))
            xml.Add(new XAttribute("tooltip", el.ToolTip));
        
        if (!string.IsNullOrEmpty(el.Name))
        {
            xml.Add(new XText(el.Name));
        }
        if (el.Children != null)
        {
            foreach (var child in el.Children)
            {
                xml.Add(BuildXmlElement(child));
            }
        }
        return xml;
    }

    private static ScreenElement BuildElement(AutomationElement el)
    {
        try
        {
            // Skip offscreen elements
            if (el.Properties.IsOffscreen.IsSupported && el.Properties.IsOffscreen.TryGetValue(out bool off) && off)
                return null;

            // Skip minimized windows - check pattern support properly
            if (el.Patterns.Window.IsSupported)
            {
                try
                {
                    var windowPattern = el.Patterns.Window.Pattern;
                    if (windowPattern != null && windowPattern.WindowVisualState == WindowVisualState.Minimized)
                        return null;
                }
                catch
                {
                    // Pattern might be supported but not accessible, continue processing
                }
            }

            // Read properties safely
            string name = string.Empty;
            if (el.Properties.Name.IsSupported && el.Properties.Name.TryGetValue(out var n))
                name = n;
                
            // Read IsEnabled property
            bool? isEnabled = null;
            if (el.Properties.IsEnabled.IsSupported && el.Properties.IsEnabled.TryGetValue(out bool enabled))
                isEnabled = enabled;
                
            // Read HasKeyboardFocus property
            bool? hasKeyboardFocus = null;
            if (el.Properties.HasKeyboardFocus.IsSupported && el.Properties.HasKeyboardFocus.TryGetValue(out bool focus))
                hasKeyboardFocus = focus;
                
            // Read HelpText property (often used as tooltip)
            string toolTip = null;
            if (el.Properties.HelpText.IsSupported && el.Properties.HelpText.TryGetValue(out var help) && !string.IsNullOrWhiteSpace(help))
                toolTip = help;

            var rectInfo = el.BoundingRectangle;
            var rect = new Rect
            {
                X = rectInfo.Left,
                Y = rectInfo.Top,
                W = rectInfo.Width,
                H = rectInfo.Height
            };

            var node = new ScreenElement
            {
                ControlType = el.ControlType.ToString(),
                Name = name,
                Rect = rect,
                IsEnabled = isEnabled,
                HasKeyboardFocus = hasKeyboardFocus,
                ToolTip = toolTip,
                Children = new List<ScreenElement>()
            };

            foreach (var child in el.FindAllChildren())
            {
                try
                {
                    var childNode = BuildElement(child);
                    if (childNode != null)
                        node.Children.Add(childNode);
                }
                catch
                {
                    // Skip children that can't be processed
                }
            }
            return node;
        }
        catch
        {
            // If any error occurs processing this element, skip it
            return null;
        }
    }
}

// Internal POCOs
internal class ScreenElement
{
    public string ControlType { get; set; }
    public string Name { get; set; }
    public Rect Rect { get; set; }
    public bool? IsEnabled { get; set; }
    public bool? HasKeyboardFocus { get; set; }
    public string ToolTip { get; set; }
    public List<ScreenElement> Children { get; set; }
}

internal class Rect
{
    public double X { get; set; }
    public double Y { get; set; }
    public double W { get; set; }
    public double H { get; set; }
}