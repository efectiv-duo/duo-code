using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using duo_code.Models;

namespace duo_code.Services;

public class OpenAiApiService : IApiService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl = "https://api.openai.com/v1/chat/completions";
    private readonly duo_code.Services.RateLimits _rateLimiter;

    public OpenAiApiService(string apiKey)
    {
        _apiKey = apiKey;
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(5);

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        
        // Get rate limiter instance for this API key - OpenAI typically has combined token limits
        _rateLimiter = duo_code.Services.RateLimiters.Get($"openai_{apiKey}", 60_000, 60_000);
    }

    public async Task<ProcessedResponse> GetAISuggestionAsync(
        List<CerebrasMessage> messages,
        CancellationToken cancellationToken = default,
        string? model = null,
        ConsoleInterface? console = null)
    {
        var usedModel = model ?? CurrentState.Model;
        var request = OpenAIMessageConvertor.ConvertToOpenAiRequest(messages, usedModel);

        if (request.MaxTokens is null or > 2048)
            request.MaxTokens = 1024;

        // Estimate token usage for rate limiting check
        var estimatedInputTokens = EstimateInputTokens(messages);
        var estimatedOutputTokens = Math.Min(request.MaxTokens ?? 1024, 1024);

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
            var jsonSettings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Ignore
            };

            var requestJson = JsonConvert.SerializeObject(request, jsonSettings);
            using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(_baseUrl, content, cancellationToken);

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

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                console?.ShowError($"OpenAI API error: {response.StatusCode}\n{errorContent}");
                response.EnsureSuccessStatusCode();
            }

            // Update rate limiter from response headers - now handles all OpenAI header formats
            _rateLimiter.UpdateFromHeaders(response);

            var responseJson = await response.Content.ReadAsStringAsync();
            var openAIResponse = JsonConvert.DeserializeObject<OpenAIResponse>(responseJson);

            var processedResponse = new ProcessedResponse();

            if (openAIResponse?.Choices?.FirstOrDefault()?.Message?.Content != null)
            {
                processedResponse.Content = openAIResponse.Choices.FirstOrDefault()?.Message?.Content ?? "";
            }

            // Handle token usage - display actual usage from response
            if (openAIResponse?.Usage != null)
            {
                var usage = openAIResponse.Usage;
                processedResponse.OutputTokensCount = usage.CompletionTokens;
                
                // For OpenAI, update the rate limiter with actual individual usage
                // but track total consumption correctly for shared pool
                _rateLimiter.UpdateOpenAIActualUsage(usage.PromptTokens, usage.CompletionTokens);
                
                // Display usage info
                _rateLimiter.DisplayUsageInfo(
                    usage.PromptTokens,
                    usage.CompletionTokens,
                    console
                );
            }
            else
            {
                _rateLimiter.ShowNoUsageInfo(console);
            }

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

    public void Dispose()
    {
        _httpClient?.Dispose();
    }

    private static int EstimateInputTokens(List<CerebrasMessage> messages)
    {
        var chars = messages?.Sum(m => m?.Content?.Length ?? 0) ?? 0;
        var approx = Math.Max(1, chars / 4);
        var overhead = (messages?.Count ?? 0) * 8;
        return approx + overhead;
    }

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
}