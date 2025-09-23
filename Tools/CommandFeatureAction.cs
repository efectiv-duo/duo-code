using duo_code.Tools.Core;
using System.Text;
using System.Text.RegularExpressions;

namespace duo_code.Tools;

public class CommandFeatureAction : ToolActionBase
{
    public override string ToolName => "COMMAND_FEATURE";
    public override string Description => @"Creates or updates a command feature (Create, Update, Delete) in the backend project.
Format:
COMMAND_FEATURE: FeatureName
[Full feature code]

The feature code should:
- Include necessary using statements
- Use namespace Application.Features.[EntityName].[FeatureName]
- Include Command record implementing IRequest or IRequest<T>
- Include Validator class with FluentValidation rules
- Include Handler class with ApplicationDbContext
- Handle domain events if needed
- Use ApplicationDbContext from Application.Infrastructure.Persistence for database access

Example:
COMMAND_FEATURE: CreateProduct
using Application.Domain.Entities;
using Application.Infrastructure.Persistence;
using FluentValidation;

namespace Application.Features.Products.CreateProduct;

public record CreateProductCommand(string Name, decimal Price, int Stock) : IRequest<int>;

public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(v => v.Name)
            .MaximumLength(200)
            .NotEmpty();

        RuleFor(v => v.Price)
            .GreaterThan(0);

        RuleFor(v => v.Stock)
            .GreaterThanOrEqualTo(0);
    }
}

internal sealed class CreateProductCommandHandler(ApplicationDbContext context)
    : IRequestHandler<CreateProductCommand, int>
{
    private readonly ApplicationDbContext _context = context;

    public async Task<int> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var entity = new Product
        {
            Name = request.Name,
            Price = request.Price,
            Stock = request.Stock
        };

        _context.Products.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}";

    public override bool RequiresConfirmation => true;

    public string FeatureName { get; set; } = string.Empty;
    public string FeatureCode { get; set; } = string.Empty;

    protected override string ExecuteCore(string baseDirectory)
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(FeatureName))
            return "Error: Feature name is required";

        if (!Regex.IsMatch(FeatureName, @"^[A-Z][a-zA-Z0-9]*$"))
            return $"Error: Feature name '{FeatureName}' must start with uppercase and contain only letters and numbers";

        // Find backend project
        var backendPath = FindBackendPath(baseDirectory);
        if (string.IsNullOrEmpty(backendPath))
            return "Error: Could not find backend project";

        var output = new StringBuilder();

        // Check if this is a delete operation (empty content)
        if (string.IsNullOrWhiteSpace(FeatureCode))
        {
            // Try to find and delete the feature file
            var featuresBasePath = Path.Combine(backendPath, "src", "Application", "Features");
            var deleted = false;

            // Search in all entity folders for the feature
            if (Directory.Exists(featuresBasePath))
            {
                foreach (var entityDir in Directory.GetDirectories(featuresBasePath))
                {
                    var featureToDelete = Path.Combine(entityDir, $"{FeatureName}.cs");
                    if (File.Exists(featureToDelete))
                    {
                        File.Delete(featureToDelete);
                        var entity = Path.GetFileName(entityDir);
                        output.AppendLine($"✓ Deleted command feature: Features/{entity}/{FeatureName}.cs");
                        ConsoleResultMessage = $"Command feature '{FeatureName}' deleted";
                        deleted = true;
                        break;
                    }
                }
            }

            if (!deleted)
            {
                output.AppendLine($"Command feature file not found: {FeatureName}");
                ConsoleResultMessage = $"Command feature '{FeatureName}' not found";
            }
            return output.ToString();
        }

        // Extract entity name from namespace in the code
        var entityName = ExtractEntityName(FeatureCode);
        if (string.IsNullOrEmpty(entityName))
            return "Error: Could not determine entity name from feature code namespace";

        // Create feature directory and file
        var featuresPath = Path.Combine(backendPath, "src", "Application", "Features", entityName);
        Directory.CreateDirectory(featuresPath);

        var featureFilePath = Path.Combine(featuresPath, $"{FeatureName}.cs");
        var isUpdate = File.Exists(featureFilePath);

        File.WriteAllText(featureFilePath, FeatureCode);
        output.AppendLine($"✓ {(isUpdate ? "Updated" : "Created")} command feature: Features/{entityName}/{FeatureName}.cs");

        ConsoleResultMessage = $"Command feature '{FeatureName}' processed";
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

    private string ExtractEntityName(string featureCode)
    {
        // Extract from namespace pattern: Application.Features.[EntityName].[FeatureName]
        var match = Regex.Match(featureCode, @"namespace\s+Application\.Features\.(\w+)\.");
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    public override string ToString() => $"{ToolName}: {FeatureName}";
}