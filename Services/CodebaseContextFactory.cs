using duo_code.Models;

namespace duo_code.Services;

public class CodebaseContextFactory
{
    private readonly string _projectRoot;

    public CodebaseContextFactory(string projectRoot)
    {
        _projectRoot = projectRoot;
    }

    public CodebaseContext CreateContext()
    {
        var context = new CodebaseContext();
        var dir = new DirectoryInfo(_projectRoot);

        // Common directories to ignore (not including dot directories which are handled separately)
        var ignoredDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "node_modules", "vendor", "packages", "bin", "obj",
            "dist", "build", "out", "target", "__pycache__", 
            "venv", "env", "bower_components", "jspm_packages", 
            "pkg", "Pods", "deps", "_build"
        };

        // Gather important files (config, entry points, etc.)
        context.ImportantFiles = GetImportantFiles(dir, ignoredDirs, 3);

        // Gather directories up to 2 levels deep (excluding ignored ones)
        context.Directories = GetDirectoriesUpToLevel(dir, ignoredDirs, 3);

        // Estimate project size more efficiently
        context.FileCount = EstimateFileCount(dir, ignoredDirs);

        return context;
    }

    private List<FileMetadata> GetImportantFiles(DirectoryInfo dir, HashSet<string> ignoredDirs, int maxDepth)
    {
        // Group files by type for better organization
        var fileGroups = new Dictionary<string, List<string>>
        {
            { "config", new List<string> { "package.json", "Cargo.toml", "pom.xml", "requirements.txt", "go.mod", "build.gradle", "composer.json", "Gemfile", "*.csproj", "*.sln" } },
            { "docs", new List<string> { "README.md", "README.txt", "README", "DUOCODE.md" } },
            { "build", new List<string> { "Makefile", "Dockerfile", "docker-compose.yml", ".gitlab-ci.yml", ".github/workflows/*.yml" } },
            { "entry", new List<string> { "Program.cs", "main.py", "main.js", "index.js", "app.js", "main.go", "main.rs", "App.java" } }
        };

        var result = new List<FileMetadata>();
        var processedFiles = new HashSet<string>(); // Avoid duplicates

        // Build list of directories to search based on maxDepth
        var dirsToSearch = GetDirectoriesToSearch(dir, ignoredDirs, maxDepth);

        foreach (var searchDir in dirsToSearch)
        {
            foreach (var group in fileGroups)
            {
                foreach (var pattern in group.Value)
                {
                    try
                    {
                        var files = pattern.Contains('*') 
                            ? searchDir.GetFiles(pattern, SearchOption.TopDirectoryOnly)
                            : searchDir.GetFiles(pattern, SearchOption.TopDirectoryOnly);
                        
                        foreach (var f in files)
                        {
                            // Create relative path for better context
                            var relativePath = Path.GetRelativePath(dir.FullName, f.FullName);
                            
                            if (!processedFiles.Contains(relativePath))
                            {
                                processedFiles.Add(relativePath);
                                result.Add(new FileMetadata
                                {
                                    Name = relativePath.Replace('\\', '/'), // Use forward slashes for consistency
                                    FirstLines = ReadFirstNLines(f.FullName, 5),
                                    TotalLines = CountLines(f.FullName)
                                });
                            }
                        }
                    }
                    catch
                    {
                        // Ignore pattern matching errors
                    }
                }
            }
        }

        return result.OrderBy(f => f.Name).ToList(); // Sort for consistency
    }

    private List<string> ReadFirstNLines(string filePath, int n)
    {
        try
        {
            return File.ReadLines(filePath).Take(n).ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    private int CountLines(string filePath)
    {
        try
        {
            return File.ReadLines(filePath).Count();
        }
        catch
        {
            return 0;
        }
    }

    private int EstimateFileCount(DirectoryInfo dir, HashSet<string> ignoredDirs)
    {
        // For efficiency, stop counting after 500 files
        const int maxCount = 99999;
        int count = 0;
        
        try
        {
            count = CountFilesRecursive(dir, ignoredDirs, maxCount);
        }
        catch
        {
            // If we can't enumerate, estimate based on top-level
            count = dir.GetFiles().Length * 10; // Rough estimate
        }
        
        return count;
    }
    
    private int CountFilesRecursive(DirectoryInfo dir, HashSet<string> ignoredDirs, int maxCount, int currentCount = 0)
    {
        if (currentCount >= maxCount) return maxCount;
        
        // Count files in current directory
        try
        {
            currentCount += dir.GetFiles().Length;
            if (currentCount >= maxCount) return maxCount;
            
            // Recursively count in subdirectories
            foreach (var subDir in dir.GetDirectories())
            {
                if (!ShouldIgnoreDirectory(subDir.Name, ignoredDirs))
                {
                    currentCount = CountFilesRecursive(subDir, ignoredDirs, maxCount, currentCount);
                    if (currentCount >= maxCount) return maxCount;
                }
            }
        }
        catch { /* Ignore access errors */ }
        
        return currentCount;
    }

    private List<string> GetDirectoriesUpToLevel(DirectoryInfo rootDir, HashSet<string> ignoredDirs, int maxLevel)
    {
        var result = new List<string>();
        var queue = new Queue<(DirectoryInfo dir, int level, string path)>();
        
        // Start with subdirectories of root (level 1)
        try
        {
            foreach (var subDir in rootDir.GetDirectories())
            {
                if (!ShouldIgnoreDirectory(subDir.Name, ignoredDirs))
                {
                    queue.Enqueue((subDir, 1, subDir.Name));
                }
            }
        }
        catch { /* Ignore access errors */ }
        
        // Process directories breadth-first
        while (queue.Count > 0)
        {
            var (currentDir, level, path) = queue.Dequeue();
            result.Add(path);
            
            // Add subdirectories if we haven't reached max level
            if (level < maxLevel)
            {
                try
                {
                    foreach (var subDir in currentDir.GetDirectories())
                    {
                        if (!ShouldIgnoreDirectory(subDir.Name, ignoredDirs))
                        {
                            var newPath = path + "/" + subDir.Name;
                            queue.Enqueue((subDir, level + 1, newPath));
                        }
                    }
                }
                catch { /* Ignore access errors */ }
            }
        }
        
        return result.OrderBy(d => d).ToList(); // Sort for consistency
    }

    private bool ShouldIgnoreDirectory(string dirName, HashSet<string> ignoredDirs)
    {
        return dirName.StartsWith('.') || ignoredDirs.Contains(dirName);
    }

    private List<DirectoryInfo> GetDirectoriesToSearch(DirectoryInfo rootDir, HashSet<string> ignoredDirs, int maxDepth)
    {
        var result = new List<DirectoryInfo> { rootDir }; // Always include root
        
        if (maxDepth < 1) return result;
        
        var queue = new Queue<(DirectoryInfo dir, int depth)>();
        queue.Enqueue((rootDir, 0));
        
        while (queue.Count > 0)
        {
            var (currentDir, currentDepth) = queue.Dequeue();
            
            if (currentDepth < maxDepth)
            {
                try
                {
                    foreach (var subDir in currentDir.GetDirectories())
                    {
                        if (!ShouldIgnoreDirectory(subDir.Name, ignoredDirs))
                        {
                            result.Add(subDir);
                            queue.Enqueue((subDir, currentDepth + 1));
                        }
                    }
                }
                catch { /* Ignore access errors */ }
            }
        }
        
        return result;
    }
}
