using System.Text;
using System.Text.Json;
using System.Net;
using duo_code.Commands.Actions;
using duo_code.Models;

namespace duo_code.Services;

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

    public async Task<ProcessedResponse> GetAISuggestionAsync(List<RequestMessage> messages, CancellationToken cancellationToken = default, string? model = null, ConsoleInterface? console = null)
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

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseNonStreamChatCompletion(body);
    }

    private async Task<HttpResponseMessage> TryGetAISuggestionWithModelAsync(List<RequestMessage> messages, string model, CancellationToken cancellationToken)
    {
        var request = new
        {
            model = model,
            stream = false,
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
    
    private static ProcessedResponse ParseNonStreamChatCompletion(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Extract assistant content
        string content = string.Empty;
        if (root.TryGetProperty("choices", out var choices)
            && choices.ValueKind == JsonValueKind.Array
            && choices.GetArrayLength() > 0)
        {
            var first = choices[0];

            // Primary: chat completion -> choices[0].message.content
            if (first.TryGetProperty("message", out var message)
                && message.TryGetProperty("content", out var msgContent))
            {
                content = msgContent.GetString() ?? string.Empty;
            }
            // Fallback: some providers may return 'text' field
            else if (first.TryGetProperty("text", out var textNode))
            {
                content = textNode.GetString() ?? string.Empty;
            }
        }

        // Extract completion tokens (usage.completion_tokens) if present
        int completionTokens = 0;
        if (root.TryGetProperty("usage", out var usage)
            && usage.TryGetProperty("completion_tokens", out var ct)
            && ct.ValueKind == JsonValueKind.Number
            && ct.TryGetInt32(out var ctVal))
        {
            completionTokens = ctVal;
        }

        return new ProcessedResponse
        {
            Content = content,            // full assistant message
            Thinking = string.Empty,      // keep empty unless you parse reasoning separately
            OutputTokensCount = completionTokens
        };
    }
}