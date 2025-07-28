namespace duo_code.Challenges.Categories.RealWorld;

using duo_code.Challenges.Core;

public class FindAndImplementFeatureChallenge : ChallengeBase
{
    public override int Id => 1;
    public override string Name => "Find and Implement Logging Feature";
    public override string Description => "Find where logging is configured in a large codebase and add a new custom logger";
    public override int DifficultyLevel => 75;
    public override int MaxScore => 100;
    
    public override ChallengeSetup Setup => new()
    {
        RequiredFiles = new List<FileSetup>
        {
            // Create a realistic project structure
            new FileSetup { Path = "src/Core/Logging/ILogger.cs", Content = @"namespace MyApp.Core.Logging;

public interface ILogger
{
    void LogInfo(string message);
    void LogWarning(string message);
    void LogError(string message, Exception? exception = null);
    void LogDebug(string message);
}" },
            
            new FileSetup { Path = "src/Core/Logging/ConsoleLogger.cs", Content = @"namespace MyApp.Core.Logging;

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
}" },

            new FileSetup { Path = "src/Core/Logging/LoggerFactory.cs", Content = @"namespace MyApp.Core.Logging;

public class LoggerFactory
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
}" },

            // Add many other files to make it realistic
            new FileSetup { Path = "src/Services/UserService.cs", Content = @"namespace MyApp.Services;
using MyApp.Core.Logging;

public class UserService
{
    private readonly ILogger _logger;
    
    public UserService()
    {
        _logger = LoggerFactory.CreateLogger(nameof(UserService));
    }
    
    public void CreateUser(string username)
    {
        _logger.LogInfo($""Creating user: {username}"");
        // User creation logic here
        _logger.LogInfo($""User created successfully: {username}"");
    }
}" },

            new FileSetup { Path = "src/Services/OrderService.cs", Content = @"namespace MyApp.Services;
using MyApp.Core.Logging;

public class OrderService
{
    private readonly ILogger _logger;
    
    public OrderService()
    {
        _logger = LoggerFactory.CreateLogger(nameof(OrderService));
    }
    
    public void ProcessOrder(int orderId)
    {
        _logger.LogInfo($""Processing order: {orderId}"");
        try
        {
            // Order processing logic
            _logger.LogInfo($""Order processed: {orderId}"");
        }
        catch (Exception ex)
        {
            _logger.LogError($""Failed to process order: {orderId}"", ex);
        }
    }
}" },

            // Add noise files
            new FileSetup { Path = "src/Models/User.cs", Content = "namespace MyApp.Models;\n\npublic class User { public string Id { get; set; } public string Name { get; set; } }" },
            new FileSetup { Path = "src/Models/Order.cs", Content = "namespace MyApp.Models;\n\npublic class Order { public int Id { get; set; } public decimal Total { get; set; } }" },
            new FileSetup { Path = "src/Data/DatabaseContext.cs", Content = "namespace MyApp.Data;\n\npublic class DatabaseContext { /* Database logic */ }" },
            new FileSetup { Path = "src/Controllers/HomeController.cs", Content = "namespace MyApp.Controllers;\n\npublic class HomeController { /* Controller logic */ }" },
            new FileSetup { Path = "src/Utils/StringHelpers.cs", Content = "namespace MyApp.Utils;\n\npublic static class StringHelpers { /* String utilities */ }" },
            new FileSetup { Path = "src/Utils/DateHelpers.cs", Content = "namespace MyApp.Utils;\n\npublic static class DateHelpers { /* Date utilities */ }" },
            new FileSetup { Path = "tests/UserServiceTests.cs", Content = "namespace MyApp.Tests;\n\npublic class UserServiceTests { /* Tests */ }" },
            new FileSetup { Path = "docs/README.md", Content = "# MyApp\n\nThis is a sample application." },
            new FileSetup { Path = "config/appsettings.json", Content = "{ \"ConnectionString\": \"Server=localhost;Database=MyApp\" }" }
        },
        InitialPrompt = @"You need to:
1. Find where the logging system is implemented in this codebase
2. Add a new FileLogger implementation that writes logs to files
3. The FileLogger should:
   - Implement the ILogger interface
   - Write logs to a file path specified in constructor
   - Append timestamps to each log entry
   - Create the log file if it doesn't exist
4. Update the LoggerFactory to support creating FileLoggers
5. Add a configuration option to choose between ConsoleLogger and FileLogger
6. Test your implementation by updating one of the services to use FileLogger",
        SetupInstructions = "A multi-file project has been created. You need to understand the existing logging architecture and extend it."
    };
    
    public override ChallengeVerification Verification => new()
    {
        CustomVerification = async (workingDirectory) =>
        {
            var details = new VerificationDetails();
            var requiredFiles = new[]
            {
                "src/Core/Logging/FileLogger.cs",
                "src/Core/Logging/LoggerFactory.cs"
            };
            
            // Check FileLogger implementation
            var fileLoggerPath = Path.Combine(workingDirectory, "src/Core/Logging/FileLogger.cs");
            if (File.Exists(fileLoggerPath))
            {
                details.PassedTests.Add("FileLogger.cs created");
                details.Score += 20;
                
                var content = await File.ReadAllTextAsync(fileLoggerPath);
                
                // Check if it implements ILogger
                if (content.Contains(": ILogger") || content.Contains(":ILogger"))
                {
                    details.PassedTests.Add("FileLogger implements ILogger interface");
                    details.Score += 15;
                }
                else
                {
                    details.FailedTests.Add("FileLogger doesn't implement ILogger interface");
                }
                
                // Check for file path in constructor
                if (content.Contains("string") && (content.Contains("filePath") || content.Contains("path")) && content.Contains("public FileLogger"))
                {
                    details.PassedTests.Add("FileLogger has constructor with file path parameter");
                    details.Score += 10;
                }
                else
                {
                    details.FailedTests.Add("FileLogger constructor doesn't accept file path");
                }
                
                // Check for timestamp implementation
                if (content.Contains("DateTime") || content.Contains("DateTimeOffset"))
                {
                    details.PassedTests.Add("FileLogger includes timestamp functionality");
                    details.Score += 10;
                }
                else
                {
                    details.FailedTests.Add("FileLogger doesn't include timestamps");
                }
                
                // Check for File.AppendAllText or StreamWriter
                if (content.Contains("File.") || content.Contains("StreamWriter") || content.Contains("FileStream"))
                {
                    details.PassedTests.Add("FileLogger uses file I/O operations");
                    details.Score += 10;
                }
                else
                {
                    details.FailedTests.Add("FileLogger doesn't implement file writing");
                }
            }
            else
            {
                details.FailedTests.Add("FileLogger.cs not created");
            }
            
            // Check LoggerFactory updates
            var factoryPath = Path.Combine(workingDirectory, "src/Core/Logging/LoggerFactory.cs");
            if (File.Exists(factoryPath))
            {
                var factoryContent = await File.ReadAllTextAsync(factoryPath);
                
                if (factoryContent.Contains("FileLogger"))
                {
                    details.PassedTests.Add("LoggerFactory updated to support FileLogger");
                    details.Score += 20;
                    
                    // Check for configuration logic
                    if (factoryContent.Contains("config") || factoryContent.Contains("option") || factoryContent.Contains("LoggerType"))
                    {
                        details.PassedTests.Add("LoggerFactory includes configuration logic");
                        details.Score += 15;
                    }
                    else
                    {
                        details.FailedTests.Add("LoggerFactory doesn't have configuration support");
                    }
                }
                else
                {
                    details.FailedTests.Add("LoggerFactory not updated for FileLogger");
                }
            }
            
            details.Success = details.Score >= 70;
            details.DetailedFeedback = details.Success
                ? $"Great job! You successfully implemented the FileLogger feature. Score: {details.Score}/100"
                : $"The FileLogger implementation needs more work. Score: {details.Score}/100";
            
            return details;
        }
    };
    
    protected override List<string> GenerateHints()
    {
        return new List<string>
        {
            "Use FIND to search for files containing 'ILogger' or 'interface'",
            "Look in the src/Core/Logging directory for the logging implementation",
            "The FileLogger should follow the same pattern as ConsoleLogger",
            "Don't forget to handle file creation if it doesn't exist",
            "Consider using File.AppendAllText for thread-safe file writing",
            "The LoggerFactory might need a configuration parameter or enum"
        };
    }
}