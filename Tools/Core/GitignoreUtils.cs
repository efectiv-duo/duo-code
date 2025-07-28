using System.Text;
using GitignoreParserNet;

namespace duo_code.Tools.Core;

public static class GitignoreUtils
{
    private static readonly Dictionary<string, GitignoreParser?> _gitignoreCache = new();
    
    /// <summary>
    /// Gets or loads the gitignore parser for the specified directory
    /// </summary>
    public static GitignoreParser? GetGitignoreParser(string baseDirectory)
    {
        if (_gitignoreCache.TryGetValue(baseDirectory, out var cached))
            return cached;
        
        var parser = LoadGitignore(baseDirectory);
        _gitignoreCache[baseDirectory] = parser;
        return parser;
    }
    
    /// <summary>
    /// Checks if a file should be ignored based on .gitignore rules
    /// </summary>
    public static bool ShouldIgnoreFile(string filePath, string baseDirectory)
    {
        var gitignore = GetGitignoreParser(baseDirectory);
        return ShouldIgnoreFile(filePath, baseDirectory, gitignore);
    }
    
    /// <summary>
    /// Checks if a file should be ignored using a specific gitignore parser
    /// </summary>
    public static bool ShouldIgnoreFile(string filePath, string baseDirectory, GitignoreParser? gitignore)
    {
        if (gitignore == null) return false;
        
        var relativePath = Path.GetRelativePath(baseDirectory, filePath).Replace('\\', '/');
        return gitignore.Denies(relativePath);
    }
    
    /// <summary>
    /// Clears the gitignore cache (useful when .gitignore files change)
    /// </summary>
    public static void ClearCache()
    {
        _gitignoreCache.Clear();
    }
    
    private static GitignoreParser? LoadGitignore(string baseDirectory)
    {
        try
        {
            var gitignorePath = Path.Combine(baseDirectory, ".gitignore");
            if (File.Exists(gitignorePath))
            {
                var gitignoreContent = File.ReadAllText(gitignorePath, Encoding.UTF8);
                return new GitignoreParser(gitignoreContent);
            }
        }
        catch
        {
            // If .gitignore can't be read, return null
        }
        
        return null;
    }
}