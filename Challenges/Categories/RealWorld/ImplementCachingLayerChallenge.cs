namespace duo_code.Challenges.Categories.RealWorld;

using duo_code.Challenges.Core;

public class ImplementCachingLayerChallenge : ChallengeBase
{
    public override int Id => 2;
    public override string Name => "Implement Distributed Caching Layer";
    public override string Description => "Add caching to a slow data access layer across multiple services";
    public override int DifficultyLevel => 90;
    public override int MaxScore => 100;
    
    public override ChallengeSetup Setup => new()
    {
        RequiredFiles = new List<FileSetup>
        {
            // Repository interfaces
            new FileSetup { Path = "src/Data/Interfaces/IProductRepository.cs", Content = @"namespace MyApp.Data.Interfaces;
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
}" },

            new FileSetup { Path = "src/Data/Interfaces/ICustomerRepository.cs", Content = @"namespace MyApp.Data.Interfaces;
using MyApp.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

public interface ICustomerRepository
{
    Task<Customer> GetByIdAsync(int id);
    Task<Customer> GetByEmailAsync(string email);
    Task<IEnumerable<Customer>> GetAllAsync();
    Task<IEnumerable<Customer>> GetByCountryAsync(string country);
}" },

            // Slow repository implementations
            new FileSetup { Path = "src/Data/Repositories/ProductRepository.cs", Content = @"namespace MyApp.Data.Repositories;
using MyApp.Data.Interfaces;
using MyApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class ProductRepository : IProductRepository
{
    // Simulating database access with delays
    private static readonly List<Product> _products = GenerateProducts();
    
    public async Task<Product> GetByIdAsync(int id)
    {
        // Simulating slow database query
        await Task.Delay(500);
        Console.WriteLine($""[DB] Fetching product {id} from database"");
        return _products.FirstOrDefault(p => p.Id == id);
    }
    
    public async Task<IEnumerable<Product>> GetByCategoryAsync(string category)
    {
        // Simulating slow database query
        await Task.Delay(800);
        Console.WriteLine($""[DB] Fetching products for category {category} from database"");
        return _products.Where(p => p.Category == category).ToList();
    }
    
    public async Task<IEnumerable<Product>> GetAllAsync()
    {
        // Simulating very slow query
        await Task.Delay(1500);
        Console.WriteLine(""[DB] Fetching all products from database"");
        return _products.ToList();
    }
    
    public async Task<Product> CreateAsync(Product product)
    {
        await Task.Delay(300);
        product.Id = _products.Max(p => p.Id) + 1;
        _products.Add(product);
        Console.WriteLine($""[DB] Created product {product.Id}"");
        return product;
    }
    
    public async Task<Product> UpdateAsync(Product product)
    {
        await Task.Delay(400);
        var existing = _products.FirstOrDefault(p => p.Id == product.Id);
        if (existing != null)
        {
            existing.Name = product.Name;
            existing.Price = product.Price;
            existing.Category = product.Category;
            Console.WriteLine($""[DB] Updated product {product.Id}"");
        }
        return existing;
    }
    
    public async Task<bool> DeleteAsync(int id)
    {
        await Task.Delay(300);
        var product = _products.FirstOrDefault(p => p.Id == id);
        if (product != null)
        {
            _products.Remove(product);
            Console.WriteLine($""[DB] Deleted product {id}"");
            return true;
        }
        return false;
    }
    
    private static List<Product> GenerateProducts()
    {
        return new List<Product>
        {
            new Product { Id = 1, Name = ""Laptop"", Price = 999.99m, Category = ""Electronics"" },
            new Product { Id = 2, Name = ""Mouse"", Price = 29.99m, Category = ""Electronics"" },
            new Product { Id = 3, Name = ""Keyboard"", Price = 79.99m, Category = ""Electronics"" },
            new Product { Id = 4, Name = ""Monitor"", Price = 299.99m, Category = ""Electronics"" },
            new Product { Id = 5, Name = ""Desk"", Price = 199.99m, Category = ""Furniture"" },
            new Product { Id = 6, Name = ""Chair"", Price = 149.99m, Category = ""Furniture"" }
        };
    }
}" },

            new FileSetup { Path = "src/Data/Repositories/CustomerRepository.cs", Content = @"namespace MyApp.Data.Repositories;
using MyApp.Data.Interfaces;
using MyApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class CustomerRepository : ICustomerRepository
{
    private static readonly List<Customer> _customers = GenerateCustomers();
    
    public async Task<Customer> GetByIdAsync(int id)
    {
        await Task.Delay(600);
        Console.WriteLine($""[DB] Fetching customer {id} from database"");
        return _customers.FirstOrDefault(c => c.Id == id);
    }
    
    public async Task<Customer> GetByEmailAsync(string email)
    {
        await Task.Delay(700);
        Console.WriteLine($""[DB] Fetching customer by email {email} from database"");
        return _customers.FirstOrDefault(c => c.Email == email);
    }
    
    public async Task<IEnumerable<Customer>> GetAllAsync()
    {
        await Task.Delay(2000);
        Console.WriteLine(""[DB] Fetching all customers from database"");
        return _customers.ToList();
    }
    
    public async Task<IEnumerable<Customer>> GetByCountryAsync(string country)
    {
        await Task.Delay(900);
        Console.WriteLine($""[DB] Fetching customers from {country} from database"");
        return _customers.Where(c => c.Country == country).ToList();
    }
    
    private static List<Customer> GenerateCustomers()
    {
        return new List<Customer>
        {
            new Customer { Id = 1, Name = ""John Doe"", Email = ""john@example.com"", Country = ""USA"" },
            new Customer { Id = 2, Name = ""Jane Smith"", Email = ""jane@example.com"", Country = ""UK"" },
            new Customer { Id = 3, Name = ""Bob Johnson"", Email = ""bob@example.com"", Country = ""USA"" }
        };
    }
}" },

            // Services using repositories
            new FileSetup { Path = "src/Services/ProductService.cs", Content = @"namespace MyApp.Services;
using MyApp.Data.Interfaces;
using MyApp.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

public class ProductService
{
    private readonly IProductRepository _repository;
    
    public ProductService(IProductRepository repository)
    {
        _repository = repository;
    }
    
    public async Task<Product> GetProductAsync(int id)
    {
        // This gets called frequently and is slow
        return await _repository.GetByIdAsync(id);
    }
    
    public async Task<IEnumerable<Product>> GetProductsByCategoryAsync(string category)
    {
        // This is called very often with the same categories
        return await _repository.GetByCategoryAsync(category);
    }
    
    public async Task<IEnumerable<Product>> GetAllProductsAsync()
    {
        // This is extremely slow and called often
        return await _repository.GetAllAsync();
    }
    
    public async Task<Product> UpdateProductPriceAsync(int id, decimal newPrice)
    {
        var product = await _repository.GetByIdAsync(id);
        if (product != null)
        {
            product.Price = newPrice;
            return await _repository.UpdateAsync(product);
        }
        return null;
    }
}" },

            new FileSetup { Path = "src/Services/CustomerService.cs", Content = @"namespace MyApp.Services;
using MyApp.Data.Interfaces;
using MyApp.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

public class CustomerService
{
    private readonly ICustomerRepository _repository;
    
    public CustomerService(ICustomerRepository repository)
    {
        _repository = repository;
    }
    
    public async Task<Customer> GetCustomerAsync(int id)
    {
        // Called frequently for the same customers
        return await _repository.GetByIdAsync(id);
    }
    
    public async Task<Customer> GetCustomerByEmailAsync(string email)
    {
        // Email lookups are frequent
        return await _repository.GetByEmailAsync(email);
    }
    
    public async Task<IEnumerable<Customer>> GetCustomersByCountryAsync(string country)
    {
        // Same countries queried repeatedly
        return await _repository.GetByCountryAsync(country);
    }
}" },

            // Models
            new FileSetup { Path = "src/Models/Product.cs", Content = @"namespace MyApp.Models;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public string Category { get; set; }
}" },

            new FileSetup { Path = "src/Models/Customer.cs", Content = @"namespace MyApp.Models;

public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string Country { get; set; }
}" },

            // Performance test showing the problem
            new FileSetup { Path = "tests/PerformanceTest.cs", Content = @"namespace MyApp.Tests;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using MyApp.Services;
using MyApp.Data.Repositories;

public class PerformanceTest
{
    public static async Task RunTest()
    {
        var productRepo = new ProductRepository();
        var customerRepo = new CustomerRepository();
        var productService = new ProductService(productRepo);
        var customerService = new CustomerService(customerRepo);
        
        Console.WriteLine(""Running performance test without caching..."");
        var sw = Stopwatch.StartNew();
        
        // Simulate typical usage patterns
        for (int i = 0; i < 10; i++)
        {
            // These calls are redundant but happen in real apps
            await productService.GetProductAsync(1);
            await productService.GetProductAsync(2);
            await productService.GetProductsByCategoryAsync(""Electronics"");
            await customerService.GetCustomerAsync(1);
            await customerService.GetCustomerByEmailAsync(""john@example.com"");
        }
        
        sw.Stop();
        Console.WriteLine($""Total time: {sw.ElapsedMilliseconds}ms"");
        Console.WriteLine(""With proper caching, this should be under 2000ms"");
    }
}" },

            new FileSetup { IsDirectory = true, Path = "src/Caching" }
        },
        InitialPrompt = @"This application has severe performance issues due to slow database queries being called repeatedly. You need to implement a caching layer to improve performance.

Requirements:
1. Create a generic caching interface (ICache<TKey, TValue>) in src/Caching/
2. Implement an in-memory cache with configurable expiration
3. Create cached repository decorators that wrap the existing repositories
4. The decorators should:
   - Cache read operations (Get methods)
   - Invalidate cache on write operations (Create, Update, Delete)
   - Use appropriate cache keys
   - Have configurable cache duration
5. Update the services to use the cached repositories
6. Ensure the performance test runs in under 2000ms

Cache invalidation strategy:
- GetById: Cache with key ""product_{id}"" or ""customer_{id}""
- GetByCategory: Cache with key ""products_category_{category}""
- GetByEmail: Cache with key ""customer_email_{email}""
- GetAll: Cache with key ""all_products"" or ""all_customers""
- On Update/Delete: Clear relevant cached items
- On Create: Clear ""all"" caches

The goal is to reduce redundant database calls while maintaining data consistency.",
        SetupInstructions = "A slow application with performance issues has been created. Implement caching to improve performance dramatically."
    };
    
    public override ChallengeVerification Verification => new()
    {
        CustomVerification = async (workingDirectory) =>
        {
            var details = new VerificationDetails();
            
            // Check cache interface
            var cacheInterfacePath = Path.Combine(workingDirectory, "src/Caching/ICache.cs");
            if (File.Exists(cacheInterfacePath))
            {
                details.PassedTests.Add("ICache interface created");
                details.Score += 10;
                
                var content = await File.ReadAllTextAsync(cacheInterfacePath);
                if (content.Contains("Task") && content.Contains("GetAsync") && content.Contains("SetAsync"))
                {
                    details.PassedTests.Add("Cache interface has async methods");
                    details.Score += 5;
                }
            }
            else
            {
                details.FailedTests.Add("ICache interface not found");
            }
            
            // Check cache implementation
            var cacheFiles = Directory.GetFiles(Path.Combine(workingDirectory, "src/Caching"), "*.cs", SearchOption.TopDirectoryOnly);
            var hasMemoryCache = cacheFiles.Any(f => f.Contains("MemoryCache") || f.Contains("InMemoryCache"));
            
            if (hasMemoryCache)
            {
                details.PassedTests.Add("In-memory cache implementation found");
                details.Score += 15;
                
                var cacheImpl = await File.ReadAllTextAsync(cacheFiles.First(f => f.Contains("Cache")));
                if (cacheImpl.Contains("TimeSpan") || cacheImpl.Contains("expiration") || cacheImpl.Contains("TTL"))
                {
                    details.PassedTests.Add("Cache supports expiration");
                    details.Score += 10;
                }
            }
            else
            {
                details.FailedTests.Add("No cache implementation found");
            }
            
            // Check for cached repository decorators
            var cachedProductRepo = Path.Combine(workingDirectory, "src/Data/Repositories/CachedProductRepository.cs");
            var cachedCustomerRepo = Path.Combine(workingDirectory, "src/Data/Repositories/CachedCustomerRepository.cs");
            
            int decoratorsFound = 0;
            if (File.Exists(cachedProductRepo))
            {
                decoratorsFound++;
                var content = await File.ReadAllTextAsync(cachedProductRepo);
                
                if (content.Contains("IProductRepository") && content.Contains("_cache"))
                {
                    details.PassedTests.Add("CachedProductRepository implements decorator pattern");
                    details.Score += 10;
                }
                
                if (content.Contains("InvalidateCache") || content.Contains("RemoveAsync") || content.Contains("ClearCache"))
                {
                    details.PassedTests.Add("Product cache invalidation implemented");
                    details.Score += 10;
                }
            }
            
            if (File.Exists(cachedCustomerRepo))
            {
                decoratorsFound++;
                var content = await File.ReadAllTextAsync(cachedCustomerRepo);
                
                if (content.Contains("ICustomerRepository") && content.Contains("_cache"))
                {
                    details.PassedTests.Add("CachedCustomerRepository implements decorator pattern");
                    details.Score += 10;
                }
            }
            
            if (decoratorsFound == 0)
            {
                details.FailedTests.Add("No cached repository decorators found");
            }
            else if (decoratorsFound == 1)
            {
                details.FailedTests.Add("Only one cached repository found, need both");
            }
            
            // Check if services are updated
            var productServicePath = Path.Combine(workingDirectory, "src/Services/ProductService.cs");
            if (File.Exists(productServicePath))
            {
                var content = await File.ReadAllTextAsync(productServicePath);
                if (content.Contains("Cached") || content.Contains("cache"))
                {
                    details.PassedTests.Add("ProductService updated to use caching");
                    details.Score += 10;
                }
            }
            
            // Check performance improvement
            var perfTestPath = Path.Combine(workingDirectory, "tests/PerformanceTest.cs");
            if (File.Exists(perfTestPath))
            {
                var content = await File.ReadAllTextAsync(perfTestPath);
                // In a real scenario, we'd run the test and measure time
                // For this challenge, we check if caching infrastructure is in place
                if (decoratorsFound == 2 && details.Score >= 60)
                {
                    details.PassedTests.Add("Performance likely improved with caching");
                    details.Score += 10;
                }
            }
            
            details.Success = details.Score >= 75;
            details.DetailedFeedback = details.Success
                ? $"Excellent! You've implemented a comprehensive caching solution. Score: {details.Score}/100"
                : $"Caching implementation needs more work. Focus on cache invalidation and decorator pattern. Score: {details.Score}/100";
            
            return details;
        }
    };
    
    protected override List<string> GenerateHints()
    {
        return new List<string>
        {
            "Start by creating a generic ICache<TKey, TValue> interface",
            "Use Dictionary<TKey, CacheItem<TValue>> for in-memory storage",
            "CacheItem should store the value and expiration time",
            "Decorator pattern: CachedRepository wraps the real repository",
            "In decorators, check cache first, then call underlying repository if miss",
            "Don't forget to invalidate cache entries on updates/deletes",
            "Use meaningful cache keys that won't collide",
            "Consider using SemaphoreSlim for thread-safe cache access"
        };
    }
}