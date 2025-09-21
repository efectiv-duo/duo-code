using System.Text.RegularExpressions;
using duo_code.Tools.Core;

namespace duo_code.Services;

public class FileSearchService
{
    private static readonly string[] SearchableExtensions = [
        ".cs", ".js", ".ts", ".jsx", ".tsx", ".html", ".css", ".scss", ".json", ".xml", ".txt", ".md", 
        ".yml", ".yaml", ".config", ".sql", ".py", ".java", ".cpp", ".h", ".c", ".go", ".rs", ".php",
        ".rb", ".sh", ".bat", ".ps1", ".vue", ".svelte", ".dart", ".kt", ".swift", ".scala", ".csproj",
        ".sln", ".gitignore", ".editorconfig", ".env"
    ];
    
    // Cache for progressive loading
    private string _lastSearchTerm = "";
    private string _lastBaseDirectory = "";
    private List<FileSearchResult> _allResults = new();
    
    public List<FileSearchResult> SearchFiles(string searchTerm, string baseDirectory, int maxResults = 20)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return new List<FileSearchResult>();
        }

        // Check if we need to perform a new search or can use cached results
        if (_lastSearchTerm != searchTerm || _lastBaseDirectory != baseDirectory)
        {
            _allResults = PerformFullSearch(searchTerm, baseDirectory);
            _lastSearchTerm = searchTerm;
            _lastBaseDirectory = baseDirectory;
        }
        
        // Return the requested batch
        return _allResults.Take(maxResults).ToList();
    }
    
    public FileSearchBatch GetFilesBatch(string searchTerm, string baseDirectory, int skip = 0, int take = 20)
    {
        // Ensure we have the full search results cached
        if (_lastSearchTerm != searchTerm || _lastBaseDirectory != baseDirectory)
        {
            _allResults = PerformFullSearch(searchTerm, baseDirectory);
            _lastSearchTerm = searchTerm;
            _lastBaseDirectory = baseDirectory;
        }
        
        var batch = _allResults.Skip(skip).Take(take).ToList();
        var hasMore = skip + take < _allResults.Count;
        
        return new FileSearchBatch
        {
            Results = batch,
            HasMore = hasMore,
            TotalCount = _allResults.Count,
            StartIndex = skip
        };
    }
    
    private List<FileSearchResult> PerformFullSearch(string searchTerm, string baseDirectory)
    {
        var results = new List<FileSearchResult>();
        
        try
        {
            // Get all files recursively, respecting .gitignore
            var allFiles = Directory.EnumerateFiles(baseDirectory, "*", SearchOption.AllDirectories)
                .Where(f => !GitignoreUtils.ShouldIgnoreFile(f, baseDirectory) && IsSearchableFile(f));

            foreach (var file in allFiles)
            {
                var relativePath = Path.GetRelativePath(baseDirectory, file).Replace('\\', '/');
                var fileName = Path.GetFileName(relativePath);
                
                var score = CalculateRelevanceScore(fileName, relativePath, searchTerm);
                if (score > 0)
                {
                    results.Add(new FileSearchResult
                    {
                        RelativePath = relativePath,
                        FileName = fileName,
                        RelevanceScore = score
                    });
                }
            }
        }
        catch (Exception)
        {
            // Return empty results on error - we don't want to crash the search
            return new List<FileSearchResult>();
        }

        // Sort by relevance score (highest first)
        return results
            .OrderByDescending(r => r.RelevanceScore)
            .ThenBy(r => r.RelativePath.Length) // Prefer shorter paths when scores are equal
            .ToList();
    }

    private bool IsSearchableFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        
        // Include files with searchable extensions or no extension (like README, Dockerfile, etc.)
        return SearchableExtensions.Contains(extension) || 
               string.IsNullOrEmpty(extension) ||
               Path.GetFileName(filePath).StartsWith('.'); // Config files like .gitignore
    }

    private int CalculateRelevanceScore(string fileName, string relativePath, string searchTerm)
    {
        var lowerFileName = fileName.ToLowerInvariant();
        var lowerSearchTerm = searchTerm.ToLowerInvariant();
        var lowerPath = relativePath.ToLowerInvariant();
        
        int score = 0;

        // Exact filename match (highest score)
        if (lowerFileName == lowerSearchTerm)
            score += 1000;
        
        // Filename starts with search term
        else if (lowerFileName.StartsWith(lowerSearchTerm))
            score += 800;
        
        // Filename contains search term as whole word
        else if (IsWholeWordMatch(lowerFileName, lowerSearchTerm))
            score += 600;
        
        // Filename contains search term
        else if (lowerFileName.Contains(lowerSearchTerm))
            score += 400;
        
        // Path contains search term (lower priority)
        else if (lowerPath.Contains(lowerSearchTerm))
            score += 200;

        // Bonus points for common file types and patterns
        if (score > 0)
        {
            // Prefer common code files
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            if (extension == ".cs" || extension == ".js" || extension == ".ts")
                score += 50;
            
            // Prefer files in common directories
            if (relativePath.StartsWith("src/", StringComparison.OrdinalIgnoreCase) ||
                relativePath.StartsWith("app/", StringComparison.OrdinalIgnoreCase) ||
                relativePath.StartsWith("lib/", StringComparison.OrdinalIgnoreCase))
                score += 25;
            
            // Penalize deep nesting
            var depth = relativePath.Count(c => c == '/');
            score -= Math.Max(0, (depth - 2) * 10);
        }

        return score;
    }

    private bool IsWholeWordMatch(string text, string searchTerm)
    {
        var pattern = $@"\b{Regex.Escape(searchTerm)}\b";
        return Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase);
    }
}

public class FileSearchResult
{
    public string RelativePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public int RelevanceScore { get; set; }
}

public class FileSearchBatch
{
    public List<FileSearchResult> Results { get; set; } = new();
    public bool HasMore { get; set; }
    public int TotalCount { get; set; }
    public int StartIndex { get; set; }
} 