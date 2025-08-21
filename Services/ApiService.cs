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
    private readonly duo_code.Services.RateLimits _rateLimiter;
    private readonly string _apiKey;

    public ApiService(string apiKey)
    {
        _apiKey = apiKey;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        
        // Get rate limiter instance for this API key - Cerebras typically has combined token limits
        _rateLimiter = duo_code.Services.RateLimiters.Get($"cerebras_{apiKey}", 120_000, 120_000);
    }

    public async Task<ProcessedResponse> GetAISuggestionAsync(List<CerebrasMessage> messages, CancellationToken cancellationToken = default, string? model = null, ConsoleInterface? console = null)
    {
        // Use provided model or fall back to current model setting
        var targetModel = model ?? CurrentState.Model;
        
        // Estimate token usage for rate limiting check
        var estimatedInputTokens = EstimateInputTokens(messages);
        var estimatedOutputTokens = 1024; // Conservative estimate instead of max_tokens (8192)
        
        // Check rate limits before making request
        if (!_rateLimiter.CanMakeRequest(estimatedInputTokens, estimatedOutputTokens))
        {
            _rateLimiter.CheckAndShowRateLimit(estimatedInputTokens, estimatedOutputTokens, console);
            throw new HttpRequestException("Rate limit would be exceeded. Please wait before making another request.");
        }
        
        // Record the estimated usage
        _rateLimiter.RecordRequest(estimatedInputTokens, estimatedOutputTokens);
        
        try
        {
            // Try with the target model first
            var response = await TryGetAISuggestionWithModelAsync(messages, targetModel, cancellationToken);
            
            // Check if rate limited
            if (duo_code.Services.RateLimits.IsRateLimited(response))
            {
                _rateLimiter.ShowRateLimitError(response, console);
                var retryAfter = duo_code.Services.RateLimits.GetRetryAfter(response);
                var errorMessage = $"Rate limit exceeded";
                if (retryAfter.HasValue)
                {
                    errorMessage += $". Retry after {retryAfter.Value} seconds";
                }
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"{errorMessage}: {errorContent}");
            }
            
            // If we get a 429 (Too Many Requests), try with the fallback model
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                console?.ShowInfo($"Rate limit reached for {targetModel}, falling back to {CurrentState.FallbackModel}...");
                CurrentState.Model = CurrentState.FallbackModel;
                response = await TryGetAISuggestionWithModelAsync(messages, CurrentState.FallbackModel, cancellationToken);
                
                // Check rate limit again for fallback
                if (duo_code.Services.RateLimits.IsRateLimited(response))
                {
                    _rateLimiter.ShowRateLimitError(response, console);
                    var retryAfter = duo_code.Services.RateLimits.GetRetryAfter(response);
                    var errorMessage = $"Rate limit exceeded on fallback model";
                    if (retryAfter.HasValue)
                    {
                        errorMessage += $". Retry after {retryAfter.Value} seconds";
                    }
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"{errorMessage}: {errorContent}");
                }
            }
            
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                throw new UnauthorizedAccessException("Invalid API key. Please check your Cerebras API key and try again. You can get a key from https://inference.cerebras.ai/");
            }
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                console?.ShowError($"Cerebras API error: {response.StatusCode}\n{errorContent}");
                response.EnsureSuccessStatusCode();
            }
            
            var processedResponse = await StreamingResponseProcessor.ProcessAsync(response, cancellationToken, ApiProvider.Cerebras, console);
            
            // Update rate limiter from response headers - handles Cerebras headers
            _rateLimiter.UpdateFromHeaders(response);
            
            // Calculate actual token usage since Cerebras doesn't provide detailed usage info
            var actualInputTokens = EstimateInputTokens(messages);
            var actualOutputTokens = processedResponse.OutputTokensCount > 0 ? 
                processedResponse.OutputTokensCount : 
                EstimateOutputTokens(processedResponse.Content);
            
            // Update rate limiter with actual usage instead of estimates
            _rateLimiter.UpdateActualUsage(actualInputTokens, actualOutputTokens);
            
            // Display actual token usage
            _rateLimiter.DisplayUsageInfo(actualInputTokens, actualOutputTokens, console);
            
            // Show current rate limit status
            _rateLimiter.ShowCurrentRateLimits(console);
            
            return processedResponse;
        }
        catch (Exception ex)
        {
            console?.ShowError($"API request failed: {ex.Message}");
            throw;
        }
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

    private static int EstimateInputTokens(List<CerebrasMessage> messages)
    {
        var chars = messages?.Sum(m => m?.Content?.Length ?? 0) ?? 0;
        var approx = Math.Max(1, chars / 4);
        var overhead = (messages?.Count ?? 0) * 8;
        return approx + overhead;
    }

    private static int EstimateOutputTokens(string? content)
    {
        if (string.IsNullOrEmpty(content))
            return 0;
            
        var chars = content.Length;
        var approx = Math.Max(1, chars / 4);
        return approx;
    }
    
    // Interface compatibility methods
    public bool IsNearTokenLimit(int threshold = 100)
    {
        return !_rateLimiter.CanMakeRequest(threshold, threshold);
    }

    public string GetRateLimitSummary()
    {
        return _rateLimiter.GetRateLimitSummary();
    }

    public void DisplayCurrentRateLimits()
    {
        _rateLimiter.ShowStatus();
    }

    public duo_code.Services.RateLimits GetRateLimiter()
    {
        return _rateLimiter;
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}