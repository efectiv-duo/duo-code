using System.Text;
using System.Text.Json;
using System.Net;
using duo_code.Commands.Actions;
using duo_code.Models;

namespace duo_code.Services;

public class AnthropicApiService : IApiService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiUrl = "https://api.anthropic.com/v1/messages";
    private readonly duo_code.Services.RateLimits _rateLimiter;
    private readonly string _apiKey;
    
    public AnthropicApiService(string apiKey)
    {
        _apiKey = apiKey;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        
        // Get rate limiter instance for this API key
        _rateLimiter = duo_code.Services.RateLimiters.Get($"anthropic_{apiKey}", 100_000, 100_000);
    }

    public async Task<ProcessedResponse> GetAISuggestionAsync(
        List<CerebrasMessage> messages,
        CancellationToken cancellationToken = default,
        string? model = null,
        ConsoleInterface? console = null)
    {
        var targetModel = model ?? GetAnthropicModel(CurrentState.Model);
        var request = ConvertToAnthropicRequest(messages, targetModel);

        // Estimate token usage for rate limiting check
        var estimatedInputTokens = EstimateInputTokens(messages, request.System);
        var estimatedOutputTokens = Math.Min(request.MaxTokens, 4000);

        // Check rate limits before making request - RateLimits handles all display
        if (!_rateLimiter.CanMakeRequest(estimatedInputTokens, estimatedOutputTokens))
        {
            _rateLimiter.CheckAndShowRateLimit(estimatedInputTokens, estimatedOutputTokens, console);
            throw new HttpRequestException("Rate limit would be exceeded. Please wait before making another request.");
        }

        // Record the estimated usage
        _rateLimiter.RecordRequest(estimatedInputTokens, estimatedOutputTokens);

        try
        {
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };

            var requestJson = JsonSerializer.Serialize(request, jsonOptions);
            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(_apiUrl, content, cancellationToken);

            // Check if rate limited - RateLimits handles all display
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

            response.EnsureSuccessStatusCode();

            // Update rate limiter from response headers - RateLimits handles everything
            _rateLimiter.UpdateFromHeaders(response);

            var responseContent = await response.Content.ReadAsStringAsync();
            var anthropicResponse = JsonSerializer.Deserialize<AnthropicResponse>(responseContent, jsonOptions);

            var processedResponse = new ProcessedResponse();

            if (anthropicResponse?.Content != null && anthropicResponse.Content.Any())
            {
                var contentText = string.Join("", anthropicResponse.Content
                    .Where(c => c.Type == "text")
                    .Select(c => c.Text ?? ""));

                processedResponse.Content = contentText;
            }

            // Handle token usage - RateLimits handles all display and updates
            if (anthropicResponse?.Usage != null)
            {
                processedResponse.OutputTokensCount = anthropicResponse.Usage.OutputTokens;
                
                // Update rate limiter with actual usage and let it handle display
                _rateLimiter.UpdateActualUsage(
                    anthropicResponse.Usage.InputTokens,
                    anthropicResponse.Usage.OutputTokens
                );

                _rateLimiter.DisplayUsageInfo(
                    anthropicResponse.Usage.InputTokens,
                    anthropicResponse.Usage.OutputTokens,
                    console
                );
            }
            else
            {
                _rateLimiter.ShowNoUsageInfo(console);
            }

            // Show current rate limit status - RateLimits handles display
            _rateLimiter.ShowCurrentRateLimits(console);

            return processedResponse;
        }
        catch (Exception ex)
        {
            console?.ShowError($"API request failed: {ex.Message}");
            throw;
        }
    }
    private int EstimateInputTokens(List<CerebrasMessage> messages, string? systemMessage)
    {
        int totalChars = 0;
        
        if (!string.IsNullOrEmpty(systemMessage))
            totalChars += systemMessage.Length;
            
        foreach (var message in messages)
        {
            if (!string.IsNullOrEmpty(message.Content))
                totalChars += message.Content.Length;
        }
        
        var estimatedTokens = (int)(totalChars / 4.0 * 1.2); // 20% overhead
        return Math.Max(estimatedTokens, 100);
    }
    private AnthropicRequest ConvertToAnthropicRequest(List<CerebrasMessage> messages, string model)
    {
        var (anthropicMessages, systemMessage) = ConvertToAnthropicFormat(messages);

        return new AnthropicRequest
        {
            Model = model,
            MaxTokens = 8192,
            Temperature = 0.5,
            Stream = false,
            Messages = anthropicMessages,
            System = systemMessage
        };
    }
    private (List<AnthropicMessage> messages, string? systemMessage) ConvertToAnthropicFormat(List<CerebrasMessage> messages)
    {
        var anthropicMessages = new List<AnthropicMessage>();
        string? systemMessage = null;

        foreach (var message in messages)
        {
            if (message.Role == "system")
            {
                if (string.IsNullOrEmpty(systemMessage))
                {
                    systemMessage = message.Content;
                }
                else
                {
                    systemMessage += "\n\n" + message.Content;
                }
            }
            else if (message.Role == "user" || message.Role == "assistant")
            {
                anthropicMessages.Add(new AnthropicMessage
                {
                    Role = message.Role,
                    Content = message.Content
                });
            }
        }
        if (anthropicMessages.Count > 0 && anthropicMessages[0].Role != "user")
        {
            anthropicMessages.Insert(0, new AnthropicMessage
            {
                Role = "user",
                Content = "Please continue with the task."
            });
        }

        return (anthropicMessages, systemMessage);
    }

    private string GetAnthropicModel(string currentModel)
    {
        if (CurrentState.AvailableModels[ApiProvider.Anthropic].Contains(currentModel))
        {
            return currentModel;
        }
        return "claude-3-5-sonnet-20241022";
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
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