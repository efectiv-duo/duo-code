using duo_code.Tools.Core;
using System.Text;

namespace duo_code.Tools;

public class SeedAction : ToolActionBase
{
    public override string ToolName => "SEED";
    public override string Description => @"Creates, updates, or deletes the ApplicationDbContextSeed file for database seeding.
Format:
SEED:
[Full seed class code]

Empty content deletes the seed file.

The seed code should:
- Use namespace Application.Infrastructure.Persistence
- Create static class ApplicationDbContextSeed
- Include SeedSampleDataAsync method with ApplicationDbContext parameter
- Check if data exists before seeding to avoid duplicates
- Use SaveChangesAsync for database operations

Example:
SEED:
using Application.Domain.Entities;

namespace Application.Infrastructure.Persistence;

public static class ApplicationDbContextSeed
{
    public static async Task SeedSampleDataAsync(ApplicationDbContext context)
    {
        // Seed Products
        if (!context.Products.Any())
        {
            context.Products.Add(new Product
            {
                Name = ""Sample Product"",
                Price = 99.99m,
                Stock = 100
            });

            await context.SaveChangesAsync();
        }
    }
}";

    public override bool RequiresConfirmation => true;

    public string SeedCode { get; set; } = string.Empty;

    protected override string ExecuteCore(string baseDirectory)
    {
        // Find backend project
        var backendPath = FindBackendPath(baseDirectory);
        if (string.IsNullOrEmpty(backendPath))
            return "Error: Could not find backend project";

        var output = new StringBuilder();

        // Seed file path
        var seedFilePath = Path.Combine(backendPath, "src", "Application", "Infrastructure",
                                       "Persistence", "ApplicationDbContextSeed.cs");

        // Check if this is a delete operation (empty content)
        if (string.IsNullOrWhiteSpace(SeedCode))
        {
            if (File.Exists(seedFilePath))
            {
                File.Delete(seedFilePath);
                output.AppendLine("✓ Deleted seed file: ApplicationDbContextSeed.cs");
                ConsoleResultMessage = "Seed file deleted";
            }
            else
            {
                output.AppendLine("Seed file not found");
                ConsoleResultMessage = "Seed file not found";
            }
            return output.ToString();
        }

        // Create or update seed file
        var persistencePath = Path.Combine(backendPath, "src", "Application", "Infrastructure", "Persistence");
        Directory.CreateDirectory(persistencePath);

        var isUpdate = File.Exists(seedFilePath);
        File.WriteAllText(seedFilePath, SeedCode);

        output.AppendLine($"✓ {(isUpdate ? "Updated" : "Created")} seed file: ApplicationDbContextSeed.cs");

        ConsoleResultMessage = $"Seed file {(isUpdate ? "updated" : "created")}";
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

    public override string ToString() => ToolName;
}