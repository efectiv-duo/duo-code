namespace duo_code.Challenges.Categories.RealWorld;

using duo_code.Challenges.Core;

public class RefactorCodebaseChallenge : ChallengeBase
{
    public override int Id => 3;
    public override string Name => "Refactor Duplicate Code Across Services";
    public override string Description => "Find and refactor duplicate code patterns across multiple service files";
    public override int DifficultyLevel => 80;
    public override int MaxScore => 100;
    
    public override ChallengeSetup Setup => new()
    {
        RequiredFiles = new List<FileSetup>
        {
            // Services with duplicate validation and error handling code
            new FileSetup { Path = "src/Services/CustomerService.cs", Content = @"namespace MyApp.Services;
using System;
using System.Text.RegularExpressions;

public class CustomerService
{
    public void CreateCustomer(string email, string phone, string name)
    {
        // Email validation
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException(""Email cannot be empty"", nameof(email));
        }
        
        var emailRegex = new Regex(@""^[^@\s]+@[^@\s]+\.[^@\s]+$"");
        if (!emailRegex.IsMatch(email))
        {
            throw new ArgumentException(""Invalid email format"", nameof(email));
        }
        
        // Phone validation
        if (string.IsNullOrWhiteSpace(phone))
        {
            throw new ArgumentException(""Phone cannot be empty"", nameof(phone));
        }
        
        var phoneRegex = new Regex(@""^\+?[1-9]\d{1,14}$"");
        if (!phoneRegex.IsMatch(phone))
        {
            throw new ArgumentException(""Invalid phone format"", nameof(phone));
        }
        
        // Name validation
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(""Name cannot be empty"", nameof(name));
        }
        
        if (name.Length < 2 || name.Length > 100)
        {
            throw new ArgumentException(""Name must be between 2 and 100 characters"", nameof(name));
        }
        
        try
        {
            // Create customer logic
            Console.WriteLine($""Creating customer: {name}"");
            // Database operation here
        }
        catch (Exception ex)
        {
            // Error logging
            var timestamp = DateTime.UtcNow.ToString(""yyyy-MM-dd HH:mm:ss"");
            var errorMessage = $""[{timestamp}] ERROR in CustomerService.CreateCustomer: {ex.Message}"";
            Console.Error.WriteLine(errorMessage);
            
            // Write to error file
            var errorLogPath = ""logs/errors.log"";
            System.IO.File.AppendAllText(errorLogPath, errorMessage + Environment.NewLine);
            
            throw new Exception(""Failed to create customer"", ex);
        }
    }
    
    public void UpdateCustomer(int id, string email, string phone)
    {
        // Email validation (duplicate code)
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException(""Email cannot be empty"", nameof(email));
        }
        
        var emailRegex = new Regex(@""^[^@\s]+@[^@\s]+\.[^@\s]+$"");
        if (!emailRegex.IsMatch(email))
        {
            throw new ArgumentException(""Invalid email format"", nameof(email));
        }
        
        // Phone validation (duplicate code)
        if (string.IsNullOrWhiteSpace(phone))
        {
            throw new ArgumentException(""Phone cannot be empty"", nameof(phone));
        }
        
        var phoneRegex = new Regex(@""^\+?[1-9]\d{1,14}$"");
        if (!phoneRegex.IsMatch(phone))
        {
            throw new ArgumentException(""Invalid phone format"", nameof(phone));
        }
        
        try
        {
            // Update logic
            Console.WriteLine($""Updating customer {id}"");
        }
        catch (Exception ex)
        {
            // Error logging (duplicate code)
            var timestamp = DateTime.UtcNow.ToString(""yyyy-MM-dd HH:mm:ss"");
            var errorMessage = $""[{timestamp}] ERROR in CustomerService.UpdateCustomer: {ex.Message}"";
            Console.Error.WriteLine(errorMessage);
            
            var errorLogPath = ""logs/errors.log"";
            System.IO.File.AppendAllText(errorLogPath, errorMessage + Environment.NewLine);
            
            throw new Exception(""Failed to update customer"", ex);
        }
    }
}" },

            new FileSetup { Path = "src/Services/SupplierService.cs", Content = @"namespace MyApp.Services;
using System;
using System.Text.RegularExpressions;

public class SupplierService
{
    public void RegisterSupplier(string email, string phone, string companyName, string taxId)
    {
        // Email validation (duplicate code)
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException(""Email cannot be empty"", nameof(email));
        }
        
        var emailRegex = new Regex(@""^[^@\s]+@[^@\s]+\.[^@\s]+$"");
        if (!emailRegex.IsMatch(email))
        {
            throw new ArgumentException(""Invalid email format"", nameof(email));
        }
        
        // Phone validation (duplicate code)
        if (string.IsNullOrWhiteSpace(phone))
        {
            throw new ArgumentException(""Phone cannot be empty"", nameof(phone));
        }
        
        var phoneRegex = new Regex(@""^\+?[1-9]\d{1,14}$"");
        if (!phoneRegex.IsMatch(phone))
        {
            throw new ArgumentException(""Invalid phone format"", nameof(phone));
        }
        
        // Company validation
        if (string.IsNullOrWhiteSpace(companyName))
        {
            throw new ArgumentException(""Company name cannot be empty"", nameof(companyName));
        }
        
        // Tax ID validation
        if (string.IsNullOrWhiteSpace(taxId))
        {
            throw new ArgumentException(""Tax ID cannot be empty"", nameof(taxId));
        }
        
        try
        {
            // Register supplier logic
            Console.WriteLine($""Registering supplier: {companyName}"");
            // Database operation here
        }
        catch (Exception ex)
        {
            // Error logging (duplicate code)
            var timestamp = DateTime.UtcNow.ToString(""yyyy-MM-dd HH:mm:ss"");
            var errorMessage = $""[{timestamp}] ERROR in SupplierService.RegisterSupplier: {ex.Message}"";
            Console.Error.WriteLine(errorMessage);
            
            var errorLogPath = ""logs/errors.log"";
            System.IO.File.AppendAllText(errorLogPath, errorMessage + Environment.NewLine);
            
            throw new Exception(""Failed to register supplier"", ex);
        }
    }
}" },

            new FileSetup { Path = "src/Services/EmployeeService.cs", Content = @"namespace MyApp.Services;
using System;
using System.Text.RegularExpressions;

public class EmployeeService
{
    public void HireEmployee(string email, string phone, string fullName, string department)
    {
        // Email validation (duplicate code)
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException(""Email cannot be empty"", nameof(email));
        }
        
        var emailRegex = new Regex(@""^[^@\s]+@[^@\s]+\.[^@\s]+$"");
        if (!emailRegex.IsMatch(email))
        {
            throw new ArgumentException(""Invalid email format"", nameof(email));
        }
        
        // Phone validation (duplicate code)
        if (string.IsNullOrWhiteSpace(phone))
        {
            throw new ArgumentException(""Phone cannot be empty"", nameof(phone));
        }
        
        var phoneRegex = new Regex(@""^\+?[1-9]\d{1,14}$"");
        if (!phoneRegex.IsMatch(phone))
        {
            throw new ArgumentException(""Invalid phone format"", nameof(phone));
        }
        
        // Name validation (similar to customer)
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new ArgumentException(""Name cannot be empty"", nameof(fullName));
        }
        
        if (fullName.Length < 2 || fullName.Length > 100)
        {
            throw new ArgumentException(""Name must be between 2 and 100 characters"", nameof(fullName));
        }
        
        try
        {
            // Hire employee logic
            Console.WriteLine($""Hiring employee: {fullName} for {department}"");
        }
        catch (Exception ex)
        {
            // Error logging (duplicate code)
            var timestamp = DateTime.UtcNow.ToString(""yyyy-MM-dd HH:mm:ss"");
            var errorMessage = $""[{timestamp}] ERROR in EmployeeService.HireEmployee: {ex.Message}"";
            Console.Error.WriteLine(errorMessage);
            
            var errorLogPath = ""logs/errors.log"";
            System.IO.File.AppendAllText(errorLogPath, errorMessage + Environment.NewLine);
            
            throw new Exception(""Failed to hire employee"", ex);
        }
    }
}" },

            // Add some other files for noise
            new FileSetup { Path = "src/Models/Customer.cs", Content = "namespace MyApp.Models;\n\npublic class Customer { public int Id { get; set; } public string Email { get; set; } }" },
            new FileSetup { Path = "src/Models/Supplier.cs", Content = "namespace MyApp.Models;\n\npublic class Supplier { public int Id { get; set; } public string CompanyName { get; set; } }" },
            new FileSetup { Path = "src/Data/AppDbContext.cs", Content = "namespace MyApp.Data;\n\npublic class AppDbContext { /* EF Core context */ }" },
            new FileSetup { Path = "src/Controllers/ApiController.cs", Content = "namespace MyApp.Controllers;\n\npublic class ApiController { /* API endpoints */ }" },
            new FileSetup { IsDirectory = true, Path = "src/Utils" },
            new FileSetup { IsDirectory = true, Path = "logs" }
        },
        InitialPrompt = @"This codebase has significant code duplication across services. You need to:

1. Identify the duplicate validation code (email, phone, name validation) across all service files
2. Create a ValidationHelper class in src/Utils/ that centralizes these validations
3. Identify the duplicate error logging code in catch blocks
4. Create an ErrorLogger class in src/Utils/ that centralizes error logging
5. Refactor all three service files to use the new helper classes
6. Ensure all validation rules remain exactly the same
7. Make sure error messages keep the same format

The goal is to follow the DRY (Don't Repeat Yourself) principle while maintaining all existing functionality.",
        SetupInstructions = "A project with multiple services containing duplicate code has been created. Your task is to identify and refactor the duplications."
    };
    
    public override ChallengeVerification Verification => new()
    {
        CustomVerification = async (workingDirectory) =>
        {
            var details = new VerificationDetails();
            
            // Check if ValidationHelper was created
            var validationHelperPath = Path.Combine(workingDirectory, "src/Utils/ValidationHelper.cs");
            if (File.Exists(validationHelperPath))
            {
                details.PassedTests.Add("ValidationHelper.cs created");
                details.Score += 15;
                
                var validationContent = await File.ReadAllTextAsync(validationHelperPath);
                
                // Check for email validation method
                if (validationContent.Contains("ValidateEmail") || validationContent.Contains("IsValidEmail"))
                {
                    details.PassedTests.Add("Email validation method created");
                    details.Score += 10;
                }
                else
                {
                    details.FailedTests.Add("Email validation method not found");
                }
                
                // Check for phone validation method
                if (validationContent.Contains("ValidatePhone") || validationContent.Contains("IsValidPhone"))
                {
                    details.PassedTests.Add("Phone validation method created");
                    details.Score += 10;
                }
                else
                {
                    details.FailedTests.Add("Phone validation method not found");
                }
                
                // Check if methods are static (good practice for helpers)
                if (validationContent.Contains("static") && validationContent.Contains("class"))
                {
                    details.PassedTests.Add("ValidationHelper uses static methods");
                    details.Score += 5;
                }
            }
            else
            {
                details.FailedTests.Add("ValidationHelper.cs not created");
            }
            
            // Check if ErrorLogger was created
            var errorLoggerPath = Path.Combine(workingDirectory, "src/Utils/ErrorLogger.cs");
            if (File.Exists(errorLoggerPath))
            {
                details.PassedTests.Add("ErrorLogger.cs created");
                details.Score += 15;
                
                var loggerContent = await File.ReadAllTextAsync(errorLoggerPath);
                
                // Check for logging method
                if (loggerContent.Contains("LogError") || loggerContent.Contains("WriteError"))
                {
                    details.PassedTests.Add("Error logging method created");
                    details.Score += 10;
                }
                else
                {
                    details.FailedTests.Add("Error logging method not found");
                }
            }
            else
            {
                details.FailedTests.Add("ErrorLogger.cs not created");
            }
            
            // Check if services were refactored
            var services = new[] { "CustomerService.cs", "SupplierService.cs", "EmployeeService.cs" };
            int refactoredCount = 0;
            
            foreach (var service in services)
            {
                var servicePath = Path.Combine(workingDirectory, $"src/Services/{service}");
                if (File.Exists(servicePath))
                {
                    var content = await File.ReadAllTextAsync(servicePath);
                    
                    // Check if using ValidationHelper
                    if (content.Contains("ValidationHelper") || content.Contains("using MyApp.Utils"))
                    {
                        refactoredCount++;
                        details.PassedTests.Add($"{service} uses ValidationHelper");
                        details.Score += 5;
                        
                        // Check if duplicate regex code is removed
                        var regexCount = System.Text.RegularExpressions.Regex.Matches(content, @"new Regex").Count;
                        if (regexCount <= 1) // Should have few or no regex instantiations
                        {
                            details.PassedTests.Add($"{service} removed duplicate regex code");
                            details.Score += 5;
                        }
                    }
                    
                    // Check if using ErrorLogger
                    if (content.Contains("ErrorLogger"))
                    {
                        details.PassedTests.Add($"{service} uses ErrorLogger");
                        details.Score += 5;
                    }
                }
            }
            
            if (refactoredCount == 3)
            {
                details.PassedTests.Add("All services refactored");
                details.Score += 10;
            }
            else
            {
                details.FailedTests.Add($"Only {refactoredCount}/3 services were refactored");
            }
            
            details.Success = details.Score >= 75;
            details.DetailedFeedback = details.Success
                ? $"Excellent refactoring! You successfully eliminated code duplication. Score: {details.Score}/100"
                : $"Refactoring incomplete. Some duplicate code remains. Score: {details.Score}/100";
            
            return details;
        }
    };
    
    protected override List<string> GenerateHints()
    {
        return new List<string>
        {
            "Use FIND to search for duplicate patterns like 'new Regex' or 'throw new ArgumentException'",
            "Look for validation code that appears in multiple files",
            "Create helper classes in the src/Utils directory",
            "Make validation methods static for easy reuse",
            "Consider creating methods like ValidateEmail(string email) that throw exceptions",
            "The error logger should handle timestamp formatting and file writing",
            "Don't forget to add 'using MyApp.Utils;' to the service files"
        };
    }
}