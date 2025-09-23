using duo_code.Tools.Core;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace duo_code.Tools;

public class AddMigrationAction : ToolActionBase
{
    public override string ToolName => "ADD_MIGRATION";
    public override string Description => @"Creates a new Entity Framework migration.
Format:
ADD_MIGRATION: MigrationName

The migration name should describe what changed (e.g., AddProductEntity, UpdateUserSchema).

Examples:
ADD_MIGRATION: AddProductEntity
ADD_MIGRATION: UpdateUserSchema";

    public override bool RequiresConfirmation => true;

    public string MigrationName { get; set; } = string.Empty;

    protected override string ExecuteCore(string baseDirectory)
    {
        // Validate migration name
        if (string.IsNullOrWhiteSpace(MigrationName))
            return "Error: Migration name is required";

        if (!Regex.IsMatch(MigrationName, @"^[A-Za-z][A-Za-z0-9]*$"))
            return $"Error: Migration name '{MigrationName}' must contain only letters and numbers";

        // Find backend project
        var backendPath = FindBackendPath(baseDirectory);
        if (string.IsNullOrEmpty(backendPath))
            return "Error: Could not find backend project";

        var output = new StringBuilder();

        // Find the project file path for migrations
        var projectPath = FindProjectFile(backendPath);
        if (string.IsNullOrEmpty(projectPath))
            return "Error: Could not find project file for migrations";

        var projectDir = Path.GetDirectoryName(projectPath);
        if (string.IsNullOrEmpty(projectDir))
            return "Error: Invalid project path";

        try
        {
            // Add migration
            output.AppendLine($"Creating migration: {MigrationName}");
            var addMigrationResult = RunDotNetCommand($"ef migrations add {MigrationName}", projectDir);

            if (!string.IsNullOrEmpty(addMigrationResult.error))
            {
                return $"Error creating migration:\n{addMigrationResult.error}";
            }

            output.AppendLine("✓ Migration created successfully");
            if (!string.IsNullOrEmpty(addMigrationResult.output))
            {
                // Extract relevant output
                var lines = addMigrationResult.output.Split('\n')
                    .Where(l => !string.IsNullOrWhiteSpace(l) && !l.Contains("Build started") && !l.Contains("Build succeeded"))
                    .Take(5);
                foreach (var line in lines)
                {
                    output.AppendLine($"  {line.Trim()}");
                }
            }

            output.AppendLine();
            output.AppendLine("Next step: Run 'dotnet ef database update' to apply the migration");

            ConsoleResultMessage = $"Migration '{MigrationName}' created";

            return output.ToString();
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
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

    private string FindProjectFile(string backendPath)
    {
        // Look for the project file that contains DbContext (typically Application or Api project)
        var searchPaths = new[]
        {
            Path.Combine(backendPath, "src", "Application", "Application.csproj"),
            Path.Combine(backendPath, "src", "Api", "Api.csproj"),
            Path.Combine(backendPath, "src", "Infrastructure", "Infrastructure.csproj")
        };

        // Return first existing project file
        return searchPaths.FirstOrDefault(File.Exists) ?? string.Empty;
    }

    private (string output, string error) RunDotNetCommand(string arguments, string workingDirectory)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        var output = new StringBuilder();
        var error = new StringBuilder();

        process.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                output.AppendLine(e.Data);
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                error.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Wait up to 30 seconds for the command to complete
        if (!process.WaitForExit(30000))
        {
            process.Kill();
            return ("", "Command timed out after 30 seconds");
        }

        return (output.ToString(), error.ToString());
    }

    public override string ToString() => $"{ToolName}: {MigrationName}";
}