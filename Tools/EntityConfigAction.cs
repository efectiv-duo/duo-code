using duo_code.Tools.Core;
using System.Text;
using System.Text.RegularExpressions;

namespace duo_code.Tools;

public class EntityConfigAction : ToolActionBase
{
    public override string ToolName => "ENTITY_CONFIG";
    public override string Description => @"Creates or updates an entity configuration class in the backend project.
Format:
ENTITY_CONFIG: EntityName
[Full configuration class code]

The configuration code should:
- Include necessary using statements
- Use namespace Application.Infrastructure.Persistence.Configurations
- Implement IEntityTypeConfiguration<EntityName>
- Configure entity properties, relationships, and constraints

Example:
ENTITY_CONFIG: Product
using Application.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Application.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.Ignore(e => e.DomainEvents);

        builder.Property(t => t.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.Price)
            .HasPrecision(18, 2);
    }
}";

    public override bool RequiresConfirmation => true;

    public string EntityName { get; set; } = string.Empty;
    public string ConfigurationCode { get; set; } = string.Empty;

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
        if (string.IsNullOrWhiteSpace(ConfigurationCode))
        {
            // Delete configuration file
            var configToDelete = Path.Combine(backendPath, "src", "Application", "Infrastructure",
                                             "Persistence", "Configurations", $"{EntityName}Configuration.cs");
            if (File.Exists(configToDelete))
            {
                File.Delete(configToDelete);
                output.AppendLine($"✓ Deleted configuration: {EntityName}Configuration");
                ConsoleResultMessage = $"Configuration '{EntityName}Configuration' deleted";
            }
            else
            {
                output.AppendLine($"Configuration file not found: {EntityName}Configuration");
                ConsoleResultMessage = $"Configuration '{EntityName}Configuration' not found";
            }
            return output.ToString();
        }

        // Create or update configuration file
        var configPath = Path.Combine(backendPath, "src", "Application", "Infrastructure",
                                      "Persistence", "Configurations");
        Directory.CreateDirectory(configPath);

        var configFilePath = Path.Combine(configPath, $"{EntityName}Configuration.cs");
        var isUpdate = File.Exists(configFilePath);

        File.WriteAllText(configFilePath, ConfigurationCode);
        output.AppendLine($"✓ {(isUpdate ? "Updated" : "Created")} configuration: {EntityName}Configuration");

        // Update ApplicationDbContext if needed
        var dbContextPath = Path.Combine(backendPath, "src", "Application", "Infrastructure",
                                        "Persistence", "ApplicationDbContext.cs");

        if (File.Exists(dbContextPath))
        {
            var dbContextResult = UpdateDbContextConfiguration(dbContextPath, EntityName);
            if (!string.IsNullOrEmpty(dbContextResult))
                output.AppendLine(dbContextResult);
        }

        // Reminder
        output.AppendLine();
        output.AppendLine("Remember to add migration if schema changed.");

        ConsoleResultMessage = $"Configuration '{EntityName}Configuration' processed";
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

    private string? UpdateDbContextConfiguration(string dbContextPath, string entityName)
    {
        try
        {
            var content = File.ReadAllText(dbContextPath);
            var configClassName = $"{entityName}Configuration";

            // Check if configuration is already applied
            if (content.Contains($"ApplyConfiguration(new {configClassName}())") ||
                content.Contains($"modelBuilder.ApplyConfiguration<{configClassName}>"))
                return null;

            // Find OnModelCreating method
            var pattern = @"protected override void OnModelCreating\(ModelBuilder modelBuilder\)\s*{";
            var match = Regex.Match(content, pattern);

            if (!match.Success)
                return null;

            // Find where to insert (after opening brace)
            var insertPos = match.Index + match.Length;
            var newConfig = $"\n        modelBuilder.ApplyConfiguration(new {configClassName}());";

            content = content.Insert(insertPos, newConfig);

            // Add using if needed
            if (!content.Contains("using Application.Infrastructure.Persistence.Configurations;"))
            {
                var lastUsing = Regex.Matches(content, @"^using\s+.*?;$", RegexOptions.Multiline)
                                     .LastOrDefault();
                if (lastUsing != null)
                {
                    content = content.Insert(lastUsing.Index + lastUsing.Length,
                                           "\nusing Application.Infrastructure.Persistence.Configurations;");
                }
            }

            File.WriteAllText(dbContextPath, content);
            return $"✓ Added configuration to ApplicationDbContext";
        }
        catch
        {
            return null;
        }
    }

    public override string ToString() => $"{ToolName}: {EntityName}";
}