using System.Text;
using System.Text.Json;
using System.Net;
using duo_code.Commands.Actions;
using duo_code.Models;

namespace duo_code.Services;

public enum ApiProvider
{
    Cerebras,
    Gemini
}

public static class ApiSettings
{
    public static ApiProvider CurrentProvider { get; set; } = ApiProvider.Cerebras;
    public static string CurrentModel { get; set; } = "qwen-3-235b-a22b";
    public static string FallbackModel { get; set; } = "qwen-3-32b";
    
    public static readonly Dictionary<ApiProvider, List<string>> AvailableModels = new()
    {
        {
            ApiProvider.Cerebras, new List<string>
            {
                "qwen-3-235b-a22b",
                "qwen-3-32b",
                "llama-4-maverick-17b-128e-instruct",
                "llama-4-scout-17b-16e-instruct",
                "deepseek-r1-distill-llama-70b",
                "llama-3.3-70b"
            }
        },
        {
            ApiProvider.Gemini, new List<string>
            {
                "gemini-2.5-flash"
            }
        }
    };
}

public class CerebrasApiService : IApiService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiUrl = "https://api.cerebras.ai/v1/chat/completions";
    
    public CerebrasApiService(string apiKey)
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
    }
    
    public async Task<ProcessedResponse> GetAISuggestionAsync(List<CerebrasMessage> messages, CancellationToken cancellationToken = default, string? model = null, SpectreConsoleInterface? console = null)
    {
        // Use provided model or fall back to current model setting
        var targetModel = model ?? ApiSettings.CurrentModel;
        
        // Try with the target model first
        var response = await TryGetAISuggestionWithModelAsync(messages, targetModel, cancellationToken);
        
        // If we get a 429 (Too Many Requests), try with the fallback model
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            console?.ShowInfo($"Rate limit reached for {targetModel}, falling back to {ApiSettings.FallbackModel}...");

            ApiSettings.CurrentModel = ApiSettings.FallbackModel;

            response = await TryGetAISuggestionWithModelAsync(messages, ApiSettings.FallbackModel, cancellationToken);
        }
        
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            throw new UnauthorizedAccessException("Invalid API key. Please check your Cerebras API key and try again. You can get a key from https://inference.cerebras.ai/");
        }
        
        response.EnsureSuccessStatusCode();
        return await StreamingResponseProcessor.ProcessAsync(response, cancellationToken, ApiProvider.Cerebras, console);
    }
    
    private async Task<HttpResponseMessage> TryGetAISuggestionWithModelAsync(List<CerebrasMessage> messages, string model, CancellationToken cancellationToken)
    {
        var request = new
        {
            model = model,
            stream = true,
            max_tokens = 8192,
            temperature = 0.5,
            top_p = 0.95,
            messages = messages
        };

        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var requestJson = JsonSerializer.Serialize(request, jsonOptions);
        var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        return await _httpClient.PostAsync(_apiUrl, content, cancellationToken);
    }
    
    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}