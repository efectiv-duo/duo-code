using System.Text;
using System.Text.Json;
using System.Net;
using duo_code.Commands.Actions;
using duo_code.Models;
using System.Reflection;


namespace duo_code.Services;

public class AnthropicApiService : IApiService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiUrl = "https://api.anthropic.com/v1/messages";

    public RateLimitInfo? LastRateLimitInfo { get; private set; }
    public AnthropicApiService(string apiKey)
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        //_httpClient.DefaultRequestHeaders.Add("content-type", "application/json");
    }

    public async Task<ProcessedResponse> GetAISuggestionAsync(
    List<RequestMessage> messages,
    CancellationToken cancellationToken = default,
    string? model = null,
    ConsoleInterface? console = null)
    {
        var targetModel = model ?? GetAnthropicModel(CurrentState.Model);
        var request = ConvertToAnthropicRequest(messages, targetModel);

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        var requestJson = JsonSerializer.Serialize(request, jsonOptions);
        var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(_apiUrl, content, cancellationToken);

        if (AnthropicRateLimits.IsRateLimitedResponse(response))
        {
            var rateLimitInfo = AnthropicRateLimits.ParseRateLimitHeaders(response);
            LastRateLimitInfo = rateLimitInfo;

            console?.ShowError("Rate limit exceeded!");
            rateLimitInfo.DisplayRateLimitInfo();

            var errorContent = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Rate limit exceeded: {errorContent}");
        }

        response.EnsureSuccessStatusCode();

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

        var rateLimits = AnthropicRateLimits.ParseRateLimitHeaders(response);
        LastRateLimitInfo = rateLimits;

        if (anthropicResponse?.Usage != null)
        {
            processedResponse.OutputTokensCount = anthropicResponse.Usage.OutputTokens;

            AnthropicRateLimits.DisplayCompleteUsageInfo(anthropicResponse.Usage, rateLimits, console);
        }
        else
        {
            rateLimits.DisplayRateLimitInfo();
            rateLimits.ShowLimitWarnings(console);
        }


        return processedResponse;
    }


    private AnthropicRequest ConvertToAnthropicRequest(List<RequestMessage> messages, string model)
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

    private (List<AnthropicMessage> messages, string? systemMessage) ConvertToAnthropicFormat(List<RequestMessage> messages)
    {
        var anthropicMessages = new List<AnthropicMessage>();
        string? systemMessage = null;

        foreach (var message in messages)
        {
            if (message.Role == "system")
            {
                // Anthropic handles system messages separately
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

        // Ensure conversation starts with user message
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
        // Use the model as-is if it's a valid Anthropic model
        if (CurrentState.AvailableModels[ApiProvider.Anthropic].Contains(currentModel))
        {
            return currentModel;
        }

        // Fallback to default Anthropic model
        return "claude-3-5-sonnet-20241022";
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
    public bool IsNearTokenLimit(int threshold = 100)
    {
        return LastRateLimitInfo?.IsNearTokenLimit(threshold) ?? false;
    }
    public string GetRateLimitSummary()
    {
        return LastRateLimitInfo?.GetRateLimitSummary() ?? "No rate limit data available";
    }
    public void DisplayCurrentRateLimits()
    {
        LastRateLimitInfo?.DisplayRateLimitInfo();
    }
}
