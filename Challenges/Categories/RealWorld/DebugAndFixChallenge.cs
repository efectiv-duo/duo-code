namespace duo_code.Challenges.Categories.RealWorld;

using duo_code.Challenges.Core;

public class DebugAndFixChallenge : ChallengeBase
{
    public override int Id => 4;
    public override string Name => "Debug and Fix Broken API Integration";
    public override string Description => "Find why API calls are failing and fix the integration issues across multiple files";
    public override int DifficultyLevel => 85;
    public override int MaxScore => 100;
    
    public override ChallengeSetup Setup => new()
    {
        RequiredFiles = new List<FileSetup>
        {
            // Main API client with subtle bugs
            new FileSetup { Path = "src/Integration/ApiClient.cs", Content = @"namespace MyApp.Integration;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public class ApiClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private string _apiKey;
    
    public ApiClient(string baseUrl, string apiKey)
    {
        _baseUrl = baseUrl;
        _apiKey = apiKey;
        _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri(baseUrl);
        // BUG: Missing API key header
    }
    
    public async Task<T> GetAsync<T>(string endpoint)
    {
        var response = await _httpClient.GetAsync(endpoint);
        
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            // BUG: Wrong casing for JSON deserialization
            return JsonSerializer.Deserialize<T>(content);
        }
        
        throw new HttpRequestException($""API request failed: {response.StatusCode}"");
    }
    
    public async Task<T> PostAsync<T>(string endpoint, object data)
    {
        // BUG: Wrong content type
        var json = JsonSerializer.Serialize(data);
        var content = new StringContent(json, Encoding.UTF8, ""text/plain"");
        
        var response = await _httpClient.PostAsync(endpoint, content);
        
        if (response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(responseContent);
        }
        
        // BUG: Not logging the actual error message from API
        throw new HttpRequestException($""API request failed"");
    }
}" },

            // Service using the API client
            new FileSetup { Path = "src/Services/WeatherService.cs", Content = @"namespace MyApp.Services;
using System;
using System.Threading.Tasks;
using MyApp.Integration;
using MyApp.Models;

public class WeatherService
{
    private readonly ApiClient _apiClient;
    
    public WeatherService(string apiKey)
    {
        // BUG: Wrong base URL (missing https://)
        _apiClient = new ApiClient(""api.openweathermap.org/data/2.5"", apiKey);
    }
    
    public async Task<WeatherData> GetCurrentWeatherAsync(string city)
    {
        try
        {
            // BUG: Wrong endpoint format
            var endpoint = $""weather?q={city}"";
            return await _apiClient.GetAsync<WeatherData>(endpoint);
        }
        catch (Exception ex)
        {
            // BUG: Swallowing exception details
            throw new Exception(""Failed to get weather data"");
        }
    }
    
    public async Task<ForecastData> GetForecastAsync(string city, int days)
    {
        // BUG: API expects 'cnt' parameter, not 'days'
        var endpoint = $""forecast?q={city}&days={days}"";
        return await _apiClient.GetAsync<ForecastData>(endpoint);
    }
}" },

            // Models with naming issues
            new FileSetup { Path = "src/Models/WeatherData.cs", Content = @"namespace MyApp.Models;

public class WeatherData
{
    // BUG: Property names don't match API response (should be PascalCase for C#)
    public string name { get; set; }
    public Main main { get; set; }
    public Weather[] weather { get; set; }
    public Wind wind { get; set; }
}

public class Main
{
    public double temp { get; set; }
    public double feels_like { get; set; }
    public double temp_min { get; set; }
    public double temp_max { get; set; }
    public int pressure { get; set; }
    public int humidity { get; set; }
}

public class Weather
{
    public int id { get; set; }
    public string main { get; set; }
    public string description { get; set; }
    public string icon { get; set; }
}

public class Wind
{
    public double speed { get; set; }
    public int deg { get; set; }
}" },

            new FileSetup { Path = "src/Models/ForecastData.cs", Content = @"namespace MyApp.Models;

public class ForecastData
{
    public string cod { get; set; }
    public int message { get; set; }
    public int cnt { get; set; }
    public ForecastItem[] list { get; set; }
}

public class ForecastItem
{
    public long dt { get; set; }
    public Main main { get; set; }
    public Weather[] weather { get; set; }
}" },

            // Configuration file
            new FileSetup { Path = "src/Configuration/ApiConfig.cs", Content = @"namespace MyApp.Configuration;

public static class ApiConfig
{
    // BUG: API key should not be hardcoded
    public const string WeatherApiKey = ""YOUR_API_KEY_HERE"";
    
    // BUG: These should be configurable
    public const int DefaultTimeout = 30;
    public const int MaxRetries = 3;
}" },

            // Test file showing the errors
            new FileSetup { Path = "tests/ApiTests.cs", Content = @"namespace MyApp.Tests;
using System;
using MyApp.Services;

public class ApiTests
{
    public static async Task TestWeatherApi()
    {
        try
        {
            var weatherService = new WeatherService(""test-api-key"");
            
            // Test 1: Get current weather
            Console.WriteLine(""Testing current weather..."");
            var weather = await weatherService.GetCurrentWeatherAsync(""London"");
            Console.WriteLine($""Temperature in London: {weather.main.temp}"");
            
            // Test 2: Get forecast
            Console.WriteLine(""Testing forecast..."");
            var forecast = await weatherService.GetForecastAsync(""London"", 5);
            Console.WriteLine($""Forecast items: {forecast.cnt}"");
        }
        catch (Exception ex)
        {
            Console.WriteLine($""Test failed: {ex.Message}"");
            Console.WriteLine($""Stack trace: {ex.StackTrace}"");
        }
    }
}" },

            // Error log showing issues
            new FileSetup { Path = "logs/error.log", Content = @"2024-01-15 10:23:45 [ERROR] API request failed: 401
2024-01-15 10:23:46 [ERROR] Failed to get weather data
2024-01-15 10:24:12 [ERROR] API request failed: 400
2024-01-15 10:24:13 [ERROR] The JSON value could not be converted to MyApp.Models.WeatherData
2024-01-15 10:25:01 [ERROR] API request failed
2024-01-15 10:25:02 [ERROR] No connection could be made because the target machine actively refused it" }
        },
        InitialPrompt = @"The weather API integration is broken and showing various errors in the logs. You need to:

1. Analyze the error log to understand what's failing
2. Find and fix all bugs in the API client implementation
3. Fix the service layer issues
4. Ensure proper error handling and logging
5. Make sure the API integration works correctly

Common issues to look for:
- Missing or incorrect headers
- Wrong URLs or endpoints
- Serialization/deserialization problems
- Poor error handling
- Configuration issues

The API expects:
- Authorization header with 'appid' parameter
- Base URL should be https://api.openweathermap.org/data/2.5
- Content-Type should be application/json for POST requests
- Forecast endpoint uses 'cnt' parameter for day count",
        SetupInstructions = "A broken API integration has been set up. Multiple bugs are causing failures. Find and fix them all."
    };
    
    public override ChallengeVerification Verification => new()
    {
        CustomVerification = async (workingDirectory) =>
        {
            var details = new VerificationDetails();
            var fixedIssues = 0;
            
            // Check ApiClient fixes
            var apiClientPath = Path.Combine(workingDirectory, "src/Integration/ApiClient.cs");
            if (File.Exists(apiClientPath))
            {
                var content = await File.ReadAllTextAsync(apiClientPath);
                
                // Check if API key header is added
                if (content.Contains("Authorization") || content.Contains("appid") || content.Contains("DefaultRequestHeaders"))
                {
                    details.PassedTests.Add("API key authentication implemented");
                    details.Score += 15;
                    fixedIssues++;
                }
                else
                {
                    details.FailedTests.Add("API key header still missing");
                }
                
                // Check JSON serialization options
                if (content.Contains("PropertyNamingPolicy") || content.Contains("CamelCase") || content.Contains("JsonSerializerOptions"))
                {
                    details.PassedTests.Add("JSON serialization configured correctly");
                    details.Score += 15;
                    fixedIssues++;
                }
                else
                {
                    details.FailedTests.Add("JSON serialization not properly configured");
                }
                
                // Check content type fix
                if (content.Contains("application/json"))
                {
                    details.PassedTests.Add("Content-Type fixed to application/json");
                    details.Score += 10;
                    fixedIssues++;
                }
                else
                {
                    details.FailedTests.Add("Content-Type still incorrect");
                }
                
                // Check error handling improvement
                if (content.Contains("response.Content") && content.Contains("ReadAsStringAsync") && content.Contains("throw"))
                {
                    details.PassedTests.Add("Improved error handling with response details");
                    details.Score += 10;
                    fixedIssues++;
                }
            }
            
            // Check WeatherService fixes
            var weatherServicePath = Path.Combine(workingDirectory, "src/Services/WeatherService.cs");
            if (File.Exists(weatherServicePath))
            {
                var content = await File.ReadAllTextAsync(weatherServicePath);
                
                // Check URL fix
                if (content.Contains("https://"))
                {
                    details.PassedTests.Add("Base URL fixed with https");
                    details.Score += 10;
                    fixedIssues++;
                }
                else
                {
                    details.FailedTests.Add("Base URL still missing https");
                }
                
                // Check endpoint fix
                if (content.Contains("&appid=") || content.Contains("units="))
                {
                    details.PassedTests.Add("API endpoint parameters added");
                    details.Score += 10;
                    fixedIssues++;
                }
                
                // Check forecast parameter fix
                if (content.Contains("cnt=") && !content.Contains("days="))
                {
                    details.PassedTests.Add("Forecast parameter fixed to use 'cnt'");
                    details.Score += 10;
                    fixedIssues++;
                }
                else
                {
                    details.FailedTests.Add("Forecast still using wrong parameter name");
                }
                
                // Check exception handling
                if (content.Contains("ex.Message") || content.Contains("InnerException"))
                {
                    details.PassedTests.Add("Exception details preserved");
                    details.Score += 10;
                    fixedIssues++;
                }
            }
            
            // Check if models were updated
            var weatherDataPath = Path.Combine(workingDirectory, "src/Models/WeatherData.cs");
            if (File.Exists(weatherDataPath))
            {
                var content = await File.ReadAllTextAsync(weatherDataPath);
                
                // Check if JsonPropertyName attributes were added
                if (content.Contains("JsonPropertyName") || content.Contains("[JsonProperty"))
                {
                    details.PassedTests.Add("Model properties mapped correctly with attributes");
                    details.Score += 10;
                    fixedIssues++;
                }
                else if (content.Contains("public string Name") && !content.Contains("public string name"))
                {
                    details.PassedTests.Add("Model properties converted to PascalCase");
                    details.Score += 10;
                    fixedIssues++;
                }
            }
            
            details.Success = fixedIssues >= 7;
            details.DetailedFeedback = fixedIssues >= 9
                ? $"Excellent debugging! You fixed {fixedIssues} issues. The API integration should work now. Score: {details.Score}/100"
                : fixedIssues >= 7
                ? $"Good progress! You fixed {fixedIssues} issues. Most problems are resolved. Score: {details.Score}/100"
                : $"Keep investigating. Only {fixedIssues} issues fixed so far. Check the error logs for clues. Score: {details.Score}/100";
            
            return details;
        }
    };
    
    protected override List<string> GenerateHints()
    {
        return new List<string>
        {
            "Start by reading the error log to understand the types of failures",
            "401 errors usually mean authentication issues - check how the API key is sent",
            "400 errors often indicate malformed requests - check URLs and parameters",
            "JSON conversion errors suggest property naming mismatches",
            "Connection refused means the URL might be malformed",
            "The OpenWeatherMap API expects 'appid' as a query parameter for authentication",
            "Check if the base URL includes the protocol (https://)",
            "C# typically uses PascalCase while JSON APIs often use camelCase"
        };
    }
}