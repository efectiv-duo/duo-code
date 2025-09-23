using duo_code.Tools.Core;
using System.Text;

namespace duo_code.Tools;

public class DomainEventAction : ToolActionBase
{
    public override string ToolName => "DOMAIN_EVENT";
    public override string Description => @"Creates, updates, or deletes domain event classes in the backend project.
Format:
DOMAIN_EVENT: EventName
[Full event class code]

Empty content deletes the event file.

The event code should:
- Include necessary using statements
- Use namespace Application.Domain.Events
- Inherit from BaseEvent
- Use primary constructor syntax when appropriate
- Include properties for event data

Example:
DOMAIN_EVENT: ProductCreatedEvent
using Application.Domain.Entities;

namespace Application.Domain.Events;

internal sealed class ProductCreatedEvent(Product product) : BaseEvent
{
    public Product Product { get; } = product;
}";

    public override bool RequiresConfirmation => true;

    public string EventName { get; set; } = string.Empty;
    public string EventCode { get; set; } = string.Empty;

    protected override string ExecuteCore(string baseDirectory)
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(EventName))
            return "Error: Event name is required";

        if (!EventName.EndsWith("Event"))
            return $"Error: Event name '{EventName}' must end with 'Event'";

        // Find backend project
        var backendPath = FindBackendPath(baseDirectory);
        if (string.IsNullOrEmpty(backendPath))
            return "Error: Could not find backend project";

        var output = new StringBuilder();

        // Check if this is a delete operation (empty content)
        if (string.IsNullOrWhiteSpace(EventCode))
        {
            // Delete event file
            var eventToDelete = Path.Combine(backendPath, "src", "Application", "Domain", "Events", $"{EventName}.cs");
            if (File.Exists(eventToDelete))
            {
                File.Delete(eventToDelete);
                output.AppendLine($"✓ Deleted domain event: {EventName}");
                ConsoleResultMessage = $"Domain event '{EventName}' deleted";
            }
            else
            {
                output.AppendLine($"Domain event file not found: {EventName}");
                ConsoleResultMessage = $"Domain event '{EventName}' not found";
            }
            return output.ToString();
        }

        // Create or update event file
        var eventsPath = Path.Combine(backendPath, "src", "Application", "Domain", "Events");
        Directory.CreateDirectory(eventsPath);

        var eventFilePath = Path.Combine(eventsPath, $"{EventName}.cs");
        var isUpdate = File.Exists(eventFilePath);

        File.WriteAllText(eventFilePath, EventCode);
        output.AppendLine($"✓ {(isUpdate ? "Updated" : "Created")} domain event: Domain/Events/{EventName}.cs");

        ConsoleResultMessage = $"Domain event '{EventName}' processed";
        return output.ToString();
    }

    private string FindBackendPath(string baseDirectory)
    {
        var searchPaths = new[]
        {
            Path.Combine(baseDirectory, "projects", "backend"),
            Path.Combine(baseDirectory, "bin", "Debug", "net8.0", "projects", "backend"),
            Path.Combine(baseDirectory, "backend"),
            baseDirectory
        };

        return searchPaths.FirstOrDefault(path =>
            Directory.Exists(Path.Combine(path, "src", "Application"))) ?? string.Empty;
    }

    public override string ToString() => $"{ToolName}: {EventName}";
}