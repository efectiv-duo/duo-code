namespace duo_code.Challenges;

using duo_code.Challenges.Core;

public class ImplementCachingLayerChallenge : IChallenge
{
    public int Id => 2;
    public string Name => "Implement Distributed Caching Layer";
    public string Description => "Add caching to a slow data access layer across multiple services";
    public int DifficultyLevel => 90;
    
    public List<(string Path, string Content)> SetupFiles => new()
    {
        // Repository interfaces
        ("src/Data/Interfaces/IProductRepository.cs", @"namespace MyApp.Data.Interfaces;
using MyApp.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

public interface IProductRepository
{
    Task<Product> GetByIdAsync(int id);
    Task<IEnumerable<Product>> GetByCategoryAsync(string category);
    Task<IEnumerable<Product>> GetAllAsync();
    Task<Product> CreateAsync(Product product);
    Task<Product> UpdateAsync(Product product);
    Task<bool> DeleteAsync(int id);
}"),

        ("src/Data/Interfaces/ICustomerRepository.cs", @"namespace MyApp.Data.Interfaces;
using MyApp.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

public interface ICustomerRepository
{
    Task<Customer> GetByIdAsync(int id);
    Task<Customer> GetByEmailAsync(string email);
    Task<IEnumerable<Customer>> GetAllAsync();
    Task<IEnumerable<Customer>> GetByCountryAsync(string country);
}"),

        // Slow repository implementations
        ("src/Data/Repositories/ProductRepository.cs", @"namespace MyApp.Data.Repositories;
using MyApp.Data.Interfaces;
using MyApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class ProductRepository : IProductRepository
{
    // Simulating slow database access
    private readonly List<Product> _products = new()
    {
        new Product { Id = 1, Name = ""Laptop"", Category = ""Electronics"", Price = 999.99m },
        new Product { Id = 2, Name = ""Mouse"", Category = ""Electronics"", Price = 29.99m },
        new Product { Id = 3, Name = ""Desk"", Category = ""Furniture"", Price = 299.99m },
        new Product { Id = 4, Name = ""Chair"", Category = ""Furniture"", Price = 199.99m },
        new Product { Id = 5, Name = ""Monitor"", Category = ""Electronics"", Price = 399.99m }
    };
    
    public async Task<Product> GetByIdAsync(int id)
    {
        await Task.Delay(2000); // Simulate slow database
        return _products.FirstOrDefault(p => p.Id == id);
    }
    
    public async Task<IEnumerable<Product>> GetByCategoryAsync(string category)
    {
        await Task.Delay(3000); // Simulate slow query
        return _products.Where(p => p.Category == category);
    }
    
    public async Task<IEnumerable<Product>> GetAllAsync()
    {
        await Task.Delay(5000); // Very slow!
        return _products;
    }
    
    public async Task<Product> CreateAsync(Product product)
    {
        await Task.Delay(1000);
        product.Id = _products.Max(p => p.Id) + 1;
        _products.Add(product);
        return product;
    }
    
    public async Task<Product> UpdateAsync(Product product)
    {
        await Task.Delay(1000);
        var existing = _products.FirstOrDefault(p => p.Id == product.Id);
        if (existing != null)
        {
            existing.Name = product.Name;
            existing.Category = product.Category;
            existing.Price = product.Price;
        }
        return existing;
    }
    
    public async Task<bool> DeleteAsync(int id)
    {
        await Task.Delay(1000);
        var product = _products.FirstOrDefault(p => p.Id == id);
        if (product != null)
        {
            _products.Remove(product);
            return true;
        }
        return false;
    }
}"),

        ("src/Data/Repositories/CustomerRepository.cs", @"namespace MyApp.Data.Repositories;
using MyApp.Data.Interfaces;
using MyApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class CustomerRepository : ICustomerRepository
{
    private readonly List<Customer> _customers = new()
    {
        new Customer { Id = 1, Name = ""John Doe"", Email = ""john@example.com"", Country = ""USA"" },
        new Customer { Id = 2, Name = ""Jane Smith"", Email = ""jane@example.com"", Country = ""UK"" },
        new Customer { Id = 3, Name = ""Bob Johnson"", Email = ""bob@example.com"", Country = ""USA"" }
    };
    
    public async Task<Customer> GetByIdAsync(int id)
    {
        await Task.Delay(2000);
        return _customers.FirstOrDefault(c => c.Id == id);
    }
    
    public async Task<Customer> GetByEmailAsync(string email)
    {
        await Task.Delay(2500);
        return _customers.FirstOrDefault(c => c.Email == email);
    }
    
    public async Task<IEnumerable<Customer>> GetAllAsync()
    {
        await Task.Delay(4000);
        return _customers;
    }
    
    public async Task<IEnumerable<Customer>> GetByCountryAsync(string country)
    {
        await Task.Delay(3000);
        return _customers.Where(c => c.Country == country);
    }
}"),

        // Models
        ("src/Models/Product.cs", @"namespace MyApp.Models;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Category { get; set; }
    public decimal Price { get; set; }
}"),

        ("src/Models/Customer.cs", @"namespace MyApp.Models;

public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string Country { get; set; }
}"),

        // Services using repositories
        ("src/Services/ProductService.cs", @"namespace MyApp.Services;
using MyApp.Data.Interfaces;
using MyApp.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

public class ProductService
{
    private readonly IProductRepository _productRepository;
    
    public ProductService(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }
    
    public async Task<Product> GetProductAsync(int id)
    {
        // This is called frequently and is very slow!
        return await _productRepository.GetByIdAsync(id);
    }
    
    public async Task<IEnumerable<Product>> GetProductsByCategoryAsync(string category)
    {
        // Also frequently called and slow
        return await _productRepository.GetByCategoryAsync(category);
    }
    
    public async Task<IEnumerable<Product>> GetAllProductsAsync()
    {
        // Extremely slow operation
        return await _productRepository.GetAllAsync();
    }
}"),

        ("src/Services/CustomerService.cs", @"namespace MyApp.Services;
using MyApp.Data.Interfaces;
using MyApp.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

public class CustomerService
{
    private readonly ICustomerRepository _customerRepository;
    
    public CustomerService(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
    }
    
    public async Task<Customer> GetCustomerAsync(int id)
    {
        return await _customerRepository.GetByIdAsync(id);
    }
    
    public async Task<Customer> GetCustomerByEmailAsync(string email)
    {
        return await _customerRepository.GetByEmailAsync(email);
    }
}"),

        ("src/Configuration/CacheConfig.cs", @"namespace MyApp.Configuration;

public static class CacheConfig
{
    public static int DefaultExpirationMinutes { get; set; } = 5;
    public static bool EnableCaching { get; set; } = true;
    public static string CacheKeyPrefix { get; set; } = ""myapp"";
}")
    };
    
    public string InitialPrompt => @"The application has severe performance issues due to slow data access. Your task is to implement a caching layer to improve performance:

1. Create a generic caching interface (ICache<T>) that supports:
   - Get, Set, Remove operations
   - Key-based caching with expiration
   
2. Implement a memory cache that can be used across repositories

3. Create cached repository decorators that:
   - Cache read operations (Get methods)
   - Invalidate cache on write operations (Create, Update, Delete)
   - Use appropriate cache keys

4. The caching should:
   - Be transparent to the services
   - Use the configuration from CacheConfig
   - Handle cache misses gracefully

5. Focus on caching the most expensive operations first

Remember to follow the Decorator pattern for the cached repositories.";
    
    public async Task<ChallengeResult> VerifyAsync(string workingDirectory)
    {
        var result = new ChallengeResult();
        var points = 0;
        
        // Check for cache interface
        var cacheInterfaceFound = false;
        var possiblePaths = new[]
        {
            "src/Caching/ICache.cs",
            "src/Cache/ICache.cs",
            "src/Interfaces/ICache.cs",
            "src/Core/Caching/ICache.cs"
        };
        
        foreach (var path in possiblePaths)
        {
            var fullPath = Path.Combine(workingDirectory, path);
            if (File.Exists(fullPath))
            {
                var content = await File.ReadAllTextAsync(fullPath);
                if (content.Contains("interface ICache") && 
                    content.Contains("Get") && 
                    content.Contains("Set") && 
                    content.Contains("Remove"))
                {
                    result.PassedTests.Add("Cache interface created with required methods");
                    cacheInterfaceFound = true;
                    points++;
                    break;
                }
            }
        }
        
        if (!cacheInterfaceFound)
        {
            result.FailedTests.Add("Cache interface not found or incomplete");
        }
        
        // Check for cache implementation
        var cacheImplFound = false;
        var implPaths = new[]
        {
            "src/Caching/MemoryCache.cs",
            "src/Cache/MemoryCache.cs",
            "src/Caching/InMemoryCache.cs"
        };
        
        foreach (var path in implPaths)
        {
            var fullPath = Path.Combine(workingDirectory, path);
            if (File.Exists(fullPath))
            {
                var content = await File.ReadAllTextAsync(fullPath);
                if (content.Contains("class") && content.Contains("ICache"))
                {
                    result.PassedTests.Add("Memory cache implementation created");
                    cacheImplFound = true;
                    points++;
                    break;
                }
            }
        }
        
        // Check for cached repository decorators
        var cachedRepoCount = 0;
        var decoratorPaths = new[]
        {
            "CachedProductRepository",
            "ProductRepositoryCache",
            "CachedCustomerRepository",
            "CustomerRepositoryCache"
        };
        
        foreach (var decorator in decoratorPaths)
        {
            var found = false;
            var searchPaths = new[] { "src", "src/Data", "src/Data/Repositories", "src/Caching" };
            
            foreach (var searchPath in searchPaths)
            {
                var dir = Path.Combine(workingDirectory, searchPath);
                if (Directory.Exists(dir))
                {
                    var files = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        var content = await File.ReadAllTextAsync(file);
                        if (content.Contains($"class {decorator}") && 
                            (content.Contains("IProductRepository") || content.Contains("ICustomerRepository")))
                        {
                            found = true;
                            cachedRepoCount++;
                            break;
                        }
                    }
                }
                if (found) break;
            }
        }
        
        if (cachedRepoCount >= 2)
        {
            result.PassedTests.Add($"Cached repository decorators implemented ({cachedRepoCount} found)");
            points += 2;
        }
        else if (cachedRepoCount == 1)
        {
            result.PassedTests.Add("At least one cached repository decorator implemented");
            points++;
        }
        else
        {
            result.FailedTests.Add("No cached repository decorators found");
        }
        
        // Check for cache invalidation logic
        var invalidationFound = false;
        foreach (var searchPath in new[] { "src", "src/Data", "src/Caching" })
        {
            var dir = Path.Combine(workingDirectory, searchPath);
            if (Directory.Exists(dir))
            {
                var files = Directory.GetFiles(dir, "*Repository*.cs", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    var content = await File.ReadAllTextAsync(file);
                    if ((content.Contains("Create") || content.Contains("Update") || content.Contains("Delete")) &&
                        (content.Contains("Remove") || content.Contains("Invalidate") || content.Contains("Clear")))
                    {
                        invalidationFound = true;
                        break;
                    }
                }
            }
            if (invalidationFound) break;
        }
        
        if (invalidationFound)
        {
            result.PassedTests.Add("Cache invalidation implemented for write operations");
            points++;
        }
        else
        {
            result.FailedTests.Add("Cache invalidation not implemented");
        }
        
        // Calculate score
        result.Score = (points * 100) / 5;
        result.Success = points >= 4;
        
        result.Feedback = points >= 5
            ? "Excellent! You've implemented a complete caching solution with proper invalidation."
            : points >= 4
            ? "Good job! The caching layer is mostly complete. Minor improvements could be made."
            : $"Keep working on it. You've completed {points}/5 caching requirements.";
        
        return result;
    }
}