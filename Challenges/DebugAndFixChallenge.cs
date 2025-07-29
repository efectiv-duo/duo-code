namespace duo_code.Challenges;

using duo_code.Challenges.Core;

public class DebugAndFixChallenge : IChallenge
{
    public int Id => 4;
    public string Name => "Debug and Fix Broken API Integration";
    public string Description => "Find why API calls are failing and fix the integration issues across multiple files";
    public int DifficultyLevel => 85;
    
    public List<(string Path, string Content)> SetupFiles => new()
    {
        // Main API client with subtle bugs
        ("src/Integration/ApiClient.cs", @"namespace MyApp.Integration;
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
}"),

        // Service using the API client
        ("src/Services/WeatherService.cs", @"namespace MyApp.Services;
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
}"),

        // Models with naming issues
        ("src/Models/WeatherData.cs", @"namespace MyApp.Models;

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
}"),

        ("src/Models/ForecastData.cs", @"namespace MyApp.Models;

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
}"),

        // Configuration file
        ("src/Configuration/ApiConfig.cs", @"namespace MyApp.Configuration;

public static class ApiConfig
{
    // BUG: API key should not be hardcoded
    public const string WeatherApiKey = ""YOUR_API_KEY_HERE"";
    
    // BUG: These should be configurable
    public const int DefaultTimeout = 30;
    public const int MaxRetries = 3;
}"),

        // Test file showing the errors
        ("tests/ApiTests.cs", @"namespace MyApp.Tests;
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
}"),

        // Error log showing issues
        ("logs/error.log", @"2024-01-15 10:23:45 [ERROR] API request failed: 401
2024-01-15 10:23:46 [ERROR] Failed to get weather data
2024-01-15 10:24:12 [ERROR] API request failed: 400
2024-01-15 10:24:13 [ERROR] The JSON value could not be converted to MyApp.Models.WeatherData
2024-01-15 10:25:01 [ERROR] API request failed
2024-01-15 10:25:02 [ERROR] No connection could be made because the target machine actively refused it")
    };
    
    public string InitialPrompt => @"The weather API integration is broken and showing various errors in the logs. You need to:

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
- Forecast endpoint uses 'cnt' parameter for day count";
    
    public async Task<ChallengeResult> VerifyAsync(string workingDirectory)
    {
        var result = new ChallengeResult();
        var fixedIssues = 0;
        
        // Check ApiClient fixes
        var apiClientPath = Path.Combine(workingDirectory, "src/Integration/ApiClient.cs");
        if (File.Exists(apiClientPath))
        {
            var content = await File.ReadAllTextAsync(apiClientPath);
            
            // Check if API key header is added
            if (content.Contains("Authorization") || content.Contains("appid") || content.Contains("DefaultRequestHeaders"))
            {
                result.PassedTests.Add("API key authentication implemented");
                fixedIssues++;
            }
            else
            {
                result.FailedTests.Add("API key header still missing");
            }
            
            // Check JSON serialization options
            if (content.Contains("PropertyNamingPolicy") || content.Contains("CamelCase") || content.Contains("JsonSerializerOptions"))
            {
                result.PassedTests.Add("JSON serialization configured correctly");
                fixedIssues++;
            }
            else
            {
                result.FailedTests.Add("JSON serialization not properly configured");
            }
            
            // Check content type fix
            if (content.Contains("application/json"))
            {
                result.PassedTests.Add("Content-Type fixed to application/json");
                fixedIssues++;
            }
            else
            {
                result.FailedTests.Add("Content-Type still incorrect");
            }
            
            // Check error handling improvement
            if (content.Contains("response.Content") && content.Contains("ReadAsStringAsync") && content.Contains("throw"))
            {
                result.PassedTests.Add("Improved error handling with response details");
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
                result.PassedTests.Add("Base URL fixed with https");
                fixedIssues++;
            }
            else
            {
                result.FailedTests.Add("Base URL still missing https");
            }
            
            // Check endpoint fix
            if (content.Contains("&appid=") || content.Contains("units="))
            {
                result.PassedTests.Add("API endpoint parameters added");
                fixedIssues++;
            }
            
            // Check forecast parameter fix
            if (content.Contains("cnt=") && !content.Contains("days="))
            {
                result.PassedTests.Add("Forecast parameter fixed to use 'cnt'");
                fixedIssues++;
            }
            else
            {
                result.FailedTests.Add("Forecast still using wrong parameter name");
            }
            
            // Check exception handling
            if (content.Contains("ex.Message") || content.Contains("InnerException"))
            {
                result.PassedTests.Add("Exception details preserved");
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
                result.PassedTests.Add("Model properties mapped correctly with attributes");
                fixedIssues++;
            }
            else if (content.Contains("public string Name") && !content.Contains("public string name"))
            {
                result.PassedTests.Add("Model properties converted to PascalCase");
                fixedIssues++;
            }
        }
        
        // Calculate score
        result.Score = (fixedIssues * 100) / 10; // 10 possible fixes
        result.Success = fixedIssues >= 7;
        
        result.Feedback = fixedIssues >= 9
            ? $"Excellent debugging! You fixed {fixedIssues}/10 issues. The API integration should work now."
            : fixedIssues >= 7
            ? $"Good progress! You fixed {fixedIssues}/10 issues. Most problems are resolved."
            : $"Keep investigating. Only {fixedIssues}/10 issues fixed so far. Check the error logs for clues.";
        
        return result;
    }
}