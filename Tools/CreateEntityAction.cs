using duo_code.Tools.Core;
using System.Text;
using System.Text.RegularExpressions;

namespace duo_code.Tools;

public class CreateEntityAction : ToolActionBase
{
    public override string ToolName => "ENTITY";
    public override string Description => @"Creates or updates an entity class in the backend project and updates ApplicationDbContext.
Format:
ENTITY: EntityName
[Full entity class code]

The entity code should:
- Include necessary using statements
- Use namespace Application.Domain.Entities
- Inherit from BaseEntity (has Id and DomainEvents) or AuditableEntity (adds Created, CreatedBy, LastModified, LastModifiedBy)
- Include all properties with proper types and attributes
- Include any enums defined in the same file

Example:
ENTITY: Product
using System.ComponentModel.DataAnnotations;

namespace Application.Domain.Entities;

public class Product : AuditableEntity
{
    public string Name { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; }

    private bool _enabled;
    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (value && _enabled == true)
            {
                AddDomainEvent(new ProductEnabledEvent(this));
            }

            _enabled = value;
        }
    }
}";

    public override bool RequiresConfirmation => true;

    public string EntityName { get; set; } = string.Empty;
    public string EntityCode { get; set; } = string.Empty;

    protected override string ExecuteCore(string baseDirectory)
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(EntityName))
            return "Error: Entity name is required";

        if (!Regex.IsMatch(EntityName, @"^[A-Z][a-zA-Z0-9]*$"))
            return $"Error: Entity name '{EntityName}' must start with uppercase and contain only letters and numbers";

        // Find backend project
        var backendPath = FindBackendPath(baseDirectory);
        if (string.IsNullOrEmpty(backendPath))
            return "Error: Could not find backend project";

        var output = new StringBuilder();

        // Check if this is a delete operation (empty content)
        if (string.IsNullOrWhiteSpace(EntityCode))
        {
            // Delete entity file
            var entityToDelete = Path.Combine(backendPath, "src", "Application", "Domain", "Entities", $"{EntityName}.cs");
            if (File.Exists(entityToDelete))
            {
                File.Delete(entityToDelete);
                output.AppendLine($"✓ Deleted entity: {EntityName}");
                ConsoleResultMessage = $"Entity '{EntityName}' deleted";
            }
            else
            {
                output.AppendLine($"Entity file not found: {EntityName}");
                ConsoleResultMessage = $"Entity '{EntityName}' not found";
            }
            return output.ToString();
        }

        // Create or update entity file
        var entitiesPath = Path.Combine(backendPath, "src", "Application", "Domain", "Entities");
        Directory.CreateDirectory(entitiesPath);

        var entityFilePath = Path.Combine(entitiesPath, $"{EntityName}.cs");
        var isUpdate = File.Exists(entityFilePath);

        File.WriteAllText(entityFilePath, EntityCode);
        output.AppendLine($"✓ {(isUpdate ? "Updated" : "Created")} entity: {EntityName}");

        // Update DbContext
        var dbContextPath = Path.Combine(backendPath, "src", "Application", "Infrastructure",
                                        "Persistence", "ApplicationDbContext.cs");

        if (File.Exists(dbContextPath))
        {
            var dbContextResult = UpdateDbContext(dbContextPath, EntityName);
            if (!string.IsNullOrEmpty(dbContextResult))
                output.AppendLine(dbContextResult);
        }

        ConsoleResultMessage = $"Entity '{EntityName}' processed";
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

    private string? UpdateDbContext(string dbContextPath, string entityName)
    {
        try
        {
            var content = File.ReadAllText(dbContextPath);

            // Check if DbSet already exists
            if (content.Contains($"DbSet<{entityName}>"))
                return null; // Already exists, nothing to do

            // Find insertion point
            var dbSetPattern = @"public\s+DbSet<[\w]+>\s+[\w]+\s*.*?;";
            var matches = Regex.Matches(content, dbSetPattern);

            if (matches.Count == 0)
                return null; // No DbSets found, skip

            // Insert new DbSet after the last one
            var lastMatch = matches[matches.Count - 1];
            var insertPos = lastMatch.Index + lastMatch.Length;

            var pluralName = GetPluralName(entityName);
            var newDbSet = $"\n\n    public DbSet<{entityName}> {pluralName} => Set<{entityName}>();";

            content = content.Insert(insertPos, newDbSet);

            // Add using if needed
            if (!content.Contains("using Application.Domain.Entities;"))
            {
                var lastUsing = Regex.Matches(content, @"^using\s+.*?;$", RegexOptions.Multiline)
                                     .LastOrDefault();
                if (lastUsing != null)
                {
                    content = content.Insert(lastUsing.Index + lastUsing.Length,
                                           "\nusing Application.Domain.Entities;");
                }
            }

            File.WriteAllText(dbContextPath, content);
            return $"✓ Added DbSet<{entityName}> to ApplicationDbContext";
        }
        catch
        {
            return null; // Silently skip if update fails
        }
    }

    private string GetPluralName(string name)
    {
        // Simple pluralization
        if (name.EndsWith("y") && !"aeiou".Contains(char.ToLower(name[name.Length - 2])))
            return name[..^1] + "ies";

        if (name.EndsWith("s") || name.EndsWith("x") || name.EndsWith("ch") || name.EndsWith("sh"))
            return name + "es";

        return name + "s";
    }

    public override string ToString() => $"{ToolName}: {EntityName}";
}