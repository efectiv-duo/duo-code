namespace duo_code.Challenges;

using duo_code.Challenges.Core;

public class FindAndImplementFeatureChallenge : IChallenge
{
    public int Id => 1;
    public string Name => "Find and Implement Logging Feature";
    public string Description => "Find where logging is configured in a large codebase and add a new custom logger";
    public int DifficultyLevel => 75;
    
    public List<(string Path, string Content)> SetupFiles => new()
    {
        // Create a realistic project structure
        ("src/Core/Logging/ILogger.cs", @"namespace MyApp.Core.Logging;

public interface ILogger
{
    void LogInfo(string message);
    void LogWarning(string message);
    void LogError(string message, Exception? exception = null);
    void LogDebug(string message);
}"),
        
        ("src/Core/Logging/ConsoleLogger.cs", @"namespace MyApp.Core.Logging;

public class ConsoleLogger : ILogger
{
    private readonly string _category;
    
    public ConsoleLogger(string category)
    {
        _category = category;
    }
    
    public void LogInfo(string message) => Console.WriteLine($""[INFO] {_category}: {message}"");
    public void LogWarning(string message) => Console.WriteLine($""[WARN] {_category}: {message}"");
    public void LogError(string message, Exception? exception = null) 
    {
        Console.WriteLine($""[ERROR] {_category}: {message}"");
        if (exception != null) Console.WriteLine(exception.ToString());
    }
    public void LogDebug(string message) => Console.WriteLine($""[DEBUG] {_category}: {message}"");
}"),

        ("src/Core/Logging/LoggerFactory.cs", @"namespace MyApp.Core.Logging;

public static class LoggerFactory
{
    private static readonly Dictionary<string, ILogger> _loggers = new();
    
    public static ILogger CreateLogger(string category)
    {
        if (!_loggers.ContainsKey(category))
        {
            _loggers[category] = new ConsoleLogger(category);
        }
        return _loggers[category];
    }
    
    // TODO: Add method to register custom logger types
}"),

        ("src/Services/UserService.cs", @"namespace MyApp.Services;
using MyApp.Core.Logging;

public class UserService
{
    private readonly ILogger _logger;
    
    public UserService()
    {
        _logger = LoggerFactory.CreateLogger(""UserService"");
    }
    
    public void CreateUser(string username)
    {
        _logger.LogInfo($""Creating user: {username}"");
        // User creation logic here
        _logger.LogInfo($""User created successfully: {username}"");
    }
}"),

        ("src/Services/ProductService.cs", @"namespace MyApp.Services;
using MyApp.Core.Logging;

public class ProductService
{
    private readonly ILogger _logger;
    
    public ProductService()
    {
        _logger = LoggerFactory.CreateLogger(""ProductService"");
    }
    
    public void AddProduct(string productName, decimal price)
    {
        _logger.LogInfo($""Adding product: {productName} with price: {price}"");
        
        if (price <= 0)
        {
            _logger.LogWarning($""Invalid price for product: {productName}"");
            return;
        }
        
        // Product addition logic here
        _logger.LogInfo($""Product added successfully: {productName}"");
    }
}"),

        ("src/Configuration/AppConfig.cs", @"namespace MyApp.Configuration;

public static class AppConfig
{
    public static string LogLevel { get; set; } = ""INFO"";
    public static bool EnableFileLogging { get; set; } = false;
    public static string LogFilePath { get; set; } = ""logs/app.log"";
    
    // Other configuration settings...
}"),

        ("README.md", @"# MyApp Project

This is a sample application demonstrating our logging infrastructure.

## Project Structure
- `/src/Core` - Core functionality and interfaces
- `/src/Services` - Business logic services
- `/src/Configuration` - Application configuration

## Logging
The application uses a custom logging framework. See the Core/Logging directory for implementation details.

## TODO
- Implement file-based logging
- Add structured logging support
- Create logging configuration system"),

        // Add some noise files to make it more realistic
        ("src/Models/User.cs", @"namespace MyApp.Models;
public class User
{
    public int Id { get; set; }
    public string Username { get; set; }
    public string Email { get; set; }
}"),

        ("src/Models/Product.cs", @"namespace MyApp.Models;
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
}")
    };
    
    public string InitialPrompt => @"You need to explore this codebase and implement a file-based logger. Your tasks are:

1. Find where the logging system is implemented
2. Understand how the current logging works
3. Create a new FileLogger class that implements ILogger
4. The FileLogger should:
   - Write logs to a file specified in the constructor
   - Include timestamps in the log entries
   - Format: [TIMESTAMP] [LEVEL] Category: Message
5. Update the LoggerFactory to support creating FileLoggers
6. The factory should check AppConfig.EnableFileLogging to decide which logger to create

Make sure your implementation follows the existing patterns in the codebase.";
    
    public async Task<ChallengeResult> VerifyAsync(string workingDirectory)
    {
        var result = new ChallengeResult();
        
        // Check if FileLogger was created
        var fileLoggerPath = Path.Combine(workingDirectory, "src/Core/Logging/FileLogger.cs");
        if (!File.Exists(fileLoggerPath))
        {
            result.FailedTests.Add("FileLogger.cs not found in the expected location");
            result.Score = 0;
            result.Feedback = "FileLogger implementation not found. Make sure to create it in src/Core/Logging/";
            return result;
        }
        
        var fileLoggerContent = await File.ReadAllTextAsync(fileLoggerPath);
        
        // Check FileLogger implementation
        if (fileLoggerContent.Contains("class FileLogger") && fileLoggerContent.Contains(": ILogger"))
        {
            result.PassedTests.Add("FileLogger class created and implements ILogger");
        }
        else
        {
            result.FailedTests.Add("FileLogger doesn't properly implement ILogger");
        }
        
        // Check for file path in constructor
        if (fileLoggerContent.Contains("_filePath") || fileLoggerContent.Contains("filePath"))
        {
            result.PassedTests.Add("FileLogger accepts file path");
        }
        else
        {
            result.FailedTests.Add("FileLogger doesn't store file path");
        }
        
        // Check for timestamp inclusion
        if (fileLoggerContent.Contains("DateTime") && (fileLoggerContent.Contains("Now") || fileLoggerContent.Contains("UtcNow")))
        {
            result.PassedTests.Add("FileLogger includes timestamps");
        }
        else
        {
            result.FailedTests.Add("FileLogger doesn't include timestamps in logs");
        }
        
        // Check if all log methods are implemented
        var logMethods = new[] { "LogInfo", "LogWarning", "LogError", "LogDebug" };
        var implementedMethods = logMethods.Count(method => fileLoggerContent.Contains($"public void {method}"));
        
        if (implementedMethods == 4)
        {
            result.PassedTests.Add("All logging methods implemented");
        }
        else
        {
            result.FailedTests.Add($"Only {implementedMethods}/4 logging methods implemented");
        }
        
        // Check LoggerFactory update
        var factoryPath = Path.Combine(workingDirectory, "src/Core/Logging/LoggerFactory.cs");
        if (File.Exists(factoryPath))
        {
            var factoryContent = await File.ReadAllTextAsync(factoryPath);
            
            if (factoryContent.Contains("FileLogger") && factoryContent.Contains("AppConfig"))
            {
                result.PassedTests.Add("LoggerFactory updated to support FileLogger");
            }
            else
            {
                result.FailedTests.Add("LoggerFactory not properly updated");
            }
        }
        
        // Calculate score
        result.Score = (result.PassedTests.Count * 100) / 5;
        result.Success = result.PassedTests.Count >= 4;
        
        result.Feedback = result.Success
            ? "Great job! You successfully implemented the FileLogger and integrated it with the existing logging system."
            : $"Keep working on it. You've completed {result.PassedTests.Count}/5 requirements.";
        
        return result;
    }
}