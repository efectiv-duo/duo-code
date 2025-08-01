namespace duo_code.Challenges;

using duo_code.Challenges.Core;

public class RefactorCodebaseChallenge : IChallenge
{
    public int Id => 3;
    public string Name => "Refactor Duplicate Code Across Services";
    public string Description => "Find and refactor duplicate code patterns across multiple service files";
    public int DifficultyLevel => 80;
    
    public List<(string Path, string Content)> SetupFiles => new()
    {
        // Services with duplicate validation and error handling code
        ("src/Services/CustomerService.cs", @"namespace MyApp.Services;
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
        
        // Database operation
        try
        {
            // Save customer
            Console.WriteLine($""Creating customer: {name}"");
        }
        catch (Exception ex)
        {
            // Duplicate error logging
            Console.WriteLine($""Error occurred: {ex.StateMessage}"");
            Console.WriteLine($""Stack trace: {ex.StackTrace}"");
            Console.WriteLine($""Time: {DateTime.Now}"");
            throw;
        }
    }
}"),

        ("src/Services/OrderService.cs", @"namespace MyApp.Services;
using System;
using System.Text.RegularExpressions;

public class OrderService
{
    public void CreateOrder(string customerEmail, string customerPhone, decimal amount)
    {
        // Email validation - DUPLICATE CODE
        if (string.IsNullOrWhiteSpace(customerEmail))
        {
            throw new ArgumentException(""Email cannot be empty"", nameof(customerEmail));
        }
        
        var emailRegex = new Regex(@""^[^@\s]+@[^@\s]+\.[^@\s]+$"");
        if (!emailRegex.IsMatch(customerEmail))
        {
            throw new ArgumentException(""Invalid email format"", nameof(customerEmail));
        }
        
        // Phone validation - DUPLICATE CODE
        if (string.IsNullOrWhiteSpace(customerPhone))
        {
            throw new ArgumentException(""Phone cannot be empty"", nameof(customerPhone));
        }
        
        var phoneRegex = new Regex(@""^\+?[1-9]\d{1,14}$"");
        if (!phoneRegex.IsMatch(customerPhone))
        {
            throw new ArgumentException(""Invalid phone format"", nameof(customerPhone));
        }
        
        // Amount validation
        if (amount <= 0)
        {
            throw new ArgumentException(""Amount must be positive"", nameof(amount));
        }
        
        // Database operation
        try
        {
            // Save order
            Console.WriteLine($""Creating order for: {customerEmail}"");
        }
        catch (Exception ex)
        {
            // Duplicate error logging
            Console.WriteLine($""Error occurred: {ex.StateMessage}"");
            Console.WriteLine($""Stack trace: {ex.StackTrace}"");
            Console.WriteLine($""Time: {DateTime.Now}"");
            throw;
        }
    }
}"),

        ("src/Services/SupplierService.cs", @"namespace MyApp.Services;
using System;
using System.Text.RegularExpressions;

public class SupplierService
{
    public void RegisterSupplier(string contactEmail, string contactPhone, string companyName)
    {
        // Email validation - DUPLICATE CODE
        if (string.IsNullOrWhiteSpace(contactEmail))
        {
            throw new ArgumentException(""Email cannot be empty"", nameof(contactEmail));
        }
        
        var emailRegex = new Regex(@""^[^@\s]+@[^@\s]+\.[^@\s]+$"");
        if (!emailRegex.IsMatch(contactEmail))
        {
            throw new ArgumentException(""Invalid email format"", nameof(contactEmail));
        }
        
        // Phone validation - DUPLICATE CODE
        if (string.IsNullOrWhiteSpace(contactPhone))
        {
            throw new ArgumentException(""Phone cannot be empty"", nameof(contactPhone));
        }
        
        var phoneRegex = new Regex(@""^\+?[1-9]\d{1,14}$"");
        if (!phoneRegex.IsMatch(contactPhone))
        {
            throw new ArgumentException(""Invalid phone format"", nameof(contactPhone));
        }
        
        // Company name validation
        if (string.IsNullOrWhiteSpace(companyName))
        {
            throw new ArgumentException(""Company name cannot be empty"", nameof(companyName));
        }
        
        // Database operation
        try
        {
            // Save supplier
            Console.WriteLine($""Registering supplier: {companyName}"");
        }
        catch (Exception ex)
        {
            // Duplicate error logging
            Console.WriteLine($""Error occurred: {ex.StateMessage}"");
            Console.WriteLine($""Stack trace: {ex.StackTrace}"");
            Console.WriteLine($""Time: {DateTime.Now}"");
            throw;
        }
    }
}")
    };
    
    public string InitialPrompt => @"The codebase has significant code duplication across three service classes. Your task is to:

1. Identify the duplicate validation logic for email and phone
2. Identify the duplicate error logging pattern
3. Create appropriate abstractions to eliminate duplication
4. Refactor all three services to use the new abstractions
5. Ensure the refactored code maintains the same functionality

Consider creating:
- A validation helper/utility class
- An error logging helper
- Keep the code clean and maintainable";
    
    public async Task<ChallengeResult> VerifyAsync(string workingDirectory)
    {
        var result = new ChallengeResult();
        var refactoringPoints = 0;
        
        // Check if validation abstraction was created
        var validationFiles = new[] { "Validator.cs", "ValidationHelper.cs", "InputValidator.cs", "Validation.cs" };
        string? validationFile = null;
        
        foreach (var file in validationFiles)
        {
            var paths = new[] 
            { 
                Path.Combine(workingDirectory, "src", file),
                Path.Combine(workingDirectory, "src", "Helpers", file),
                Path.Combine(workingDirectory, "src", "Utils", file),
                Path.Combine(workingDirectory, "src", "Common", file),
                Path.Combine(workingDirectory, "src", "Shared", file)
            };
            
            foreach (var path in paths)
            {
                if (File.Exists(path))
                {
                    validationFile = path;
                    break;
                }
            }
            if (validationFile != null) break;
        }
        
        if (validationFile != null)
        {
            var content = await File.ReadAllTextAsync(validationFile);
            
            // Check for email validation method
            if (content.Contains("Email") && (content.Contains("Validate") || content.Contains("IsValid")))
            {
                result.PassedTests.Add("Email validation abstracted");
                refactoringPoints++;
            }
            
            // Check for phone validation method
            if (content.Contains("Phone") && (content.Contains("Validate") || content.Contains("IsValid")))
            {
                result.PassedTests.Add("Phone validation abstracted");
                refactoringPoints++;
            }
        }
        else
        {
            result.FailedTests.Add("No validation abstraction found");
        }
        
        // Check if error logging abstraction was created
        var loggingFiles = new[] { "Logger.cs", "ErrorLogger.cs", "LogHelper.cs", "LoggingHelper.cs" };
        string? loggingFile = null;
        
        foreach (var file in loggingFiles)
        {
            var paths = new[] 
            { 
                Path.Combine(workingDirectory, "src", file),
                Path.Combine(workingDirectory, "src", "Helpers", file),
                Path.Combine(workingDirectory, "src", "Utils", file),
                Path.Combine(workingDirectory, "src", "Common", file),
                Path.Combine(workingDirectory, "src", "Logging", file)
            };
            
            foreach (var path in paths)
            {
                if (File.Exists(path))
                {
                    loggingFile = path;
                    break;
                }
            }
            if (loggingFile != null) break;
        }
        
        if (loggingFile != null)
        {
            result.PassedTests.Add("Error logging abstracted");
            refactoringPoints++;
        }
        
        // Check if services were refactored
        var services = new[] { "CustomerService.cs", "OrderService.cs", "SupplierService.cs" };
        var refactoredServices = 0;
        
        foreach (var service in services)
        {
            var servicePath = Path.Combine(workingDirectory, "src", "Services", service);
            if (File.Exists(servicePath))
            {
                var content = await File.ReadAllTextAsync(servicePath);
                
                // Check if duplicate regex patterns are removed
                var regexCount = content.Split("new Regex").Length - 1;
                if (regexCount < 2) // Should have fewer regex instances after refactoring
                {
                    refactoredServices++;
                }
            }
        }
        
        if (refactoredServices >= 2)
        {
            result.PassedTests.Add($"Services refactored to remove duplication ({refactoredServices}/3)");
            refactoringPoints += 2;
        }
        else
        {
            result.FailedTests.Add("Services still contain duplicate code");
        }
        
        // Calculate score
        result.Score = (refactoringPoints * 100) / 5;
        result.Success = refactoringPoints >= 4;
        
        result.Feedback = refactoringPoints >= 5
            ? "Excellent refactoring! All duplicate code has been properly abstracted."
            : refactoringPoints >= 4
            ? "Good refactoring! Most duplicate code has been eliminated."
            : $"Keep working on the refactoring. Only {refactoringPoints}/5 improvements made.";
        
        return result;
    }
}