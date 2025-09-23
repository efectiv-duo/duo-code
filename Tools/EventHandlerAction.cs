using duo_code.Tools.Core;
using System.Text;
using System.Text.RegularExpressions;

namespace duo_code.Tools;

public class EventHandlerAction : ToolActionBase
{
    public override string ToolName => "EVENT_HANDLER";
    public override string Description => @"Creates, updates, or deletes domain event handlers in the backend project.
Format:
EVENT_HANDLER: EventHandlerName
[Full handler class code]

Empty content deletes the handler file.

The handler code should:
- Include necessary using statements
- Use namespace Application.Features.[EntityName].EventHandlers
- Implement INotificationHandler<DomainEventNotification<TEvent>>
- Include logging if needed
- Handle the domain event appropriately

Example:
EVENT_HANDLER: ProductCreatedEventHandler
using Application.Common.Models;
using Application.Domain.Events;
using Microsoft.Extensions.Logging;

namespace Application.Features.Products.EventHandlers;

internal sealed class ProductCreatedEventHandler(ILogger<ProductCreatedEventHandler> logger)
    : INotificationHandler<DomainEventNotification<ProductCreatedEvent>>
{
    private readonly ILogger<ProductCreatedEventHandler> _logger = logger;

    public Task Handle(DomainEventNotification<ProductCreatedEvent> notification, CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        _logger.LogInformation(""Product created: {ProductId}"", domainEvent.Product.Id);

        // Add your event handling logic here

        return Task.CompletedTask;
    }
}";

    public override bool RequiresConfirmation => true;

    public string HandlerName { get; set; } = string.Empty;
    public string HandlerCode { get; set; } = string.Empty;

    protected override string ExecuteCore(string baseDirectory)
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(HandlerName))
            return "Error: Handler name is required";

        if (!HandlerName.EndsWith("Handler"))
            return $"Error: Handler name '{HandlerName}' must end with 'Handler'";

        // Find backend project
        var backendPath = FindBackendPath(baseDirectory);
        if (string.IsNullOrEmpty(backendPath))
            return "Error: Could not find backend project";

        var output = new StringBuilder();

        // Check if this is a delete operation (empty content)
        if (string.IsNullOrWhiteSpace(HandlerCode))
        {
            // Try to find and delete the handler file
            var featuresBasePath = Path.Combine(backendPath, "src", "Application", "Features");
            var deleted = false;

            // Search in all entity folders for the handler
            if (Directory.Exists(featuresBasePath))
            {
                foreach (var entityDir in Directory.GetDirectories(featuresBasePath))
                {
                    var handlersPath = Path.Combine(entityDir, "EventHandlers");
                    if (Directory.Exists(handlersPath))
                    {
                        var handlerToDelete = Path.Combine(handlersPath, $"{HandlerName}.cs");
                        if (File.Exists(handlerToDelete))
                        {
                            File.Delete(handlerToDelete);
                            var entity = Path.GetFileName(entityDir);
                            output.AppendLine($"✓ Deleted event handler: Features/{entity}/EventHandlers/{HandlerName}.cs");
                            ConsoleResultMessage = $"Event handler '{HandlerName}' deleted";
                            deleted = true;
                            break;
                        }
                    }
                }
            }

            if (!deleted)
            {
                output.AppendLine($"Event handler file not found: {HandlerName}");
                ConsoleResultMessage = $"Event handler '{HandlerName}' not found";
            }
            return output.ToString();
        }

        // Extract entity name from namespace in the code
        var entityName = ExtractEntityName(HandlerCode);
        if (string.IsNullOrEmpty(entityName))
            return "Error: Could not determine entity name from handler code namespace";

        // Create handler directory and file
        var handlerPath = Path.Combine(backendPath, "src", "Application", "Features", entityName, "EventHandlers");
        Directory.CreateDirectory(handlerPath);

        var handlerFilePath = Path.Combine(handlerPath, $"{HandlerName}.cs");
        var isUpdate = File.Exists(handlerFilePath);

        File.WriteAllText(handlerFilePath, HandlerCode);
        output.AppendLine($"✓ {(isUpdate ? "Updated" : "Created")} event handler: Features/{entityName}/EventHandlers/{HandlerName}.cs");

        ConsoleResultMessage = $"Event handler '{HandlerName}' processed";
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

    private string ExtractEntityName(string handlerCode)
    {
        // Extract from namespace pattern: Application.Features.[EntityName].EventHandlers
        var match = Regex.Match(handlerCode, @"namespace\s+Application\.Features\.(\w+)\.EventHandlers");
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    public override string ToString() => $"{ToolName}: {HandlerName}";
}