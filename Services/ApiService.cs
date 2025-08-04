using System.Text;
using System.Text.Json;
using System.Net;
using duo_code.Commands.Actions;
using duo_code.Models;

namespace duo_code.Services;

public class ApiService : IApiService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiUrl = "https://api.cerebras.ai/v1/chat/completions";
    
    public ApiService(string apiKey)
    {
        _httpClient = new HttpClient();

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
    }
    
    public async Task<ProcessedResponse> GetAISuggestionAsync(List<CerebrasMessage> messages, CancellationToken cancellationToken = default, string? model = null, ConsoleInterface? console = null)
    {
        // Use provided model or fall back to current model setting
        var targetModel = model ?? CurrentState.Model;
        
        // Try with the target model first
        var response = await TryGetAISuggestionWithModelAsync(messages, targetModel, cancellationToken);
        
        // If we get a 429 (Too Many Requests), try with the fallback model
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            console?.ShowInfo($"Rate limit reached for {targetModel}, falling back to {CurrentState.FallbackModel}...");

            CurrentState.Model = CurrentState.FallbackModel;

            response = await TryGetAISuggestionWithModelAsync(messages, CurrentState.FallbackModel, cancellationToken);
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