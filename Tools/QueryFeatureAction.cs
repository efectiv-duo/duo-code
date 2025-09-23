using duo_code.Tools.Core;
using System.Text;
using System.Text.RegularExpressions;

namespace duo_code.Tools;

public class QueryFeatureAction : ToolActionBase
{
    public override string ToolName => "QUERY_FEATURE";
    public override string Description => @"Creates or updates a query feature with optional pagination support in the backend project.
Format:
QUERY_FEATURE: FeatureName
[Full feature code]

The feature code should:
- Include necessary using statements
- Use namespace Application.Features.[EntityName].[FeatureName]
- Include Response record/class
- Include Query record implementing IRequest
- Include Validator class if needed
- Include Handler class
- Support pagination pattern with PaginatedList<T> for list queries

Example for paginated query:
QUERY_FEATURE: GetProductsWithPagination
using Application.Common.Models;
using Application.Common.Mappings;
using Application.Domain.Entities;
using Application.Infrastructure.Persistence;

using FluentValidation;

namespace Application.Features.Products.GetProductsWithPagination;

public record ProductBriefResponse(int Id, string Name, decimal Price);

public record GetProductsWithPaginationQuery(int PageNumber = 1, int PageSize = 10)
    : IRequest<PaginatedList<ProductBriefResponse>>;

public class GetProductsWithPaginationQueryValidator : AbstractValidator<GetProductsWithPaginationQuery>
{
    public GetProductsWithPaginationQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize)
            .GreaterThanOrEqualTo(1);
    }
}

internal sealed class GetProductsWithPaginationQueryHandler(ApplicationDbContext context)
    : IRequestHandler<GetProductsWithPaginationQuery, PaginatedList<ProductBriefResponse>>
{
    private readonly ApplicationDbContext _context = context;

    public Task<PaginatedList<ProductBriefResponse>> Handle(
        GetProductsWithPaginationQuery request,
        CancellationToken cancellationToken)
    {
        return _context.Products
            .OrderBy(p => p.Name)
            .Select(p => new ProductBriefResponse(p.Id, p.Name, p.Price))
            .PaginatedListAsync(request.PageNumber, request.PageSize);
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
                        output.AppendLine($"✓ Deleted query feature: Features/{entity}/{FeatureName}.cs");
                        ConsoleResultMessage = $"Query feature '{FeatureName}' deleted";
                        deleted = true;
                        break;
                    }
                }
            }

            if (!deleted)
            {
                output.AppendLine($"Query feature file not found: {FeatureName}");
                ConsoleResultMessage = $"Query feature '{FeatureName}' not found";
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
        output.AppendLine($"✓ {(isUpdate ? "Updated" : "Created")} query feature: Features/{entityName}/{FeatureName}.cs");

        ConsoleResultMessage = $"Query feature '{FeatureName}' processed";
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