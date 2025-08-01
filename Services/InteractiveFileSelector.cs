using Spectre.Console;

namespace duo_code.Services;

public class InteractiveFileSelector
{
    private readonly FileSearchService _fileSearchService;

    public InteractiveFileSelector()
    {
        _fileSearchService = new FileSearchService();
    }

    public string? ShowFileSelection(string searchTerm, string baseDirectory)
    {
        try
        {
            // Search for files
            var searchResults = _fileSearchService.SearchFiles(searchTerm, baseDirectory, 10);
            
            if (searchResults.Count == 0)
            {
                return null; // No verbose "not found" message
            }

            // Create simple selection options
            var choices = searchResults.Select(result =>
            {
                var fileIcon = GetFileIcon(result.FileName);
                var directoryPath = Path.GetDirectoryName(result.RelativePath);
                var pathDisplay = string.IsNullOrEmpty(directoryPath) ? "" : $" [dim]({directoryPath})[/]";
                return $"{fileIcon} {result.FileName}{pathDisplay}";
            }).ToList();

            // Add a cancel option
            choices.Add("[red]Cancel[/]");

            var selection = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Select file:")
                    .PageSize(Math.Min(choices.Count, 10))
                    .AddChoices(choices));

            // Handle selection
            if (selection.Contains("Cancel"))
            {
                return null;
            }

            // Find the selected file
            var selectedIndex = choices.IndexOf(selection);
            if (selectedIndex >= 0 && selectedIndex < searchResults.Count)
            {
                return searchResults[selectedIndex].RelativePath;
            }

            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public List<FileSearchResult> GetFileResults(string searchTerm, string baseDirectory)
    {
        try
        {
            return _fileSearchService.SearchFiles(searchTerm, baseDirectory, 10);
        }
        catch
        {
            return new List<FileSearchResult>();
        }
    }

    private void ShowNoResultsMessage(string searchTerm)
    {
        var noResultsPanel = new Spectre.Console.Panel($"No files found matching '[yellow]{searchTerm}[/]'")
        {
            Header = new PanelHeader("[red]❌ No Results[/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = Style.Parse("red")
        };
        AnsiConsole.Write(noResultsPanel);
        AnsiConsole.WriteLine();
        
        AnsiConsole.WriteLine("[dim]Try a different search term or check the file name spelling.[/]");
    }

    private string GetFileIcon(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        
        return extension switch
        {
            ".cs" => "🔷", // C# files
            ".js" => "🟨", // JavaScript files  
            ".ts" => "🔷", // TypeScript files
            ".jsx" => "⚛️", // React JSX files
            ".tsx" => "⚛️", // React TSX files
            ".html" => "🌐", // HTML files
            ".css" => "🎨", // CSS files
            ".scss" => "🎨", // SCSS files
            ".json" => "📄", // JSON files
            ".md" => "📝", // Markdown files
            ".txt" => "📄", // Text files
            ".xml" => "📄", // XML files
            ".yml" or ".yaml" => "⚙️", // YAML files
            ".config" => "⚙️", // Config files
            ".sql" => "🗄️", // SQL files
            ".py" => "🐍", // Python files
            ".java" => "☕", // Java files
            ".cpp" or ".c" => "⚡", // C/C++ files
            ".h" => "📋", // Header files
            ".go" => "🔷", // Go files
            ".rs" => "🦀", // Rust files
            ".php" => "🐘", // PHP files
            ".rb" => "💎", // Ruby files
            ".sh" => "📜", // Shell scripts
            ".bat" => "📜", // Batch files
            ".ps1" => "📜", // PowerShell files
            ".csproj" => "🔧", // Project files
            ".sln" => "📦", // Solution files
            _ => "📄" // Default icon
        };
    }
} 