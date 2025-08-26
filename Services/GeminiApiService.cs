using System.Text;
using System.Text.Json;
using duo_code.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Net;

namespace duo_code.Services;

public class GeminiApiService : IApiService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";
    private readonly RateLimits _rateLimits;

    public GeminiApiService(string apiKey)
    {
        _apiKey = apiKey;
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(5);
        _rateLimits = RateLimiters.Get($"gemini_{apiKey.GetHashCode()}");

    }
    public void Dispose()
    {
        _httpClient?.Dispose();
    }

    public async Task<ProcessedResponse> GetAISuggestionAsync(List<CerebrasMessage> messages, CancellationToken cancellationToken = default, string? model = null, ConsoleInterface? console = null)
    {
        try
        {
            var apiUrl = $"{_baseUrl}?key={_apiKey}";

            // Estimate input tokens (rough estimation)
            var inputTokens = EstimateInputTokens(messages);

            // Check rate limits before making request
            _rateLimits.CheckAndShowRateLimit(inputTokens, 0, console);

            if (!_rateLimits.CanMakeRequest(inputTokens, 0))
            {
                throw new InvalidOperationException("Rate limit would be exceeded with this request");
            }

            var request = ConvertToGeminiRequest(messages);

            var jsonSettings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Ignore
            };

            var requestJson = JsonConvert.SerializeObject(request, jsonSettings);
            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            // Record the estimated request
            _rateLimits.RecordRequest(inputTokens, 0);

            var response = await _httpClient.PostAsync(apiUrl, content, cancellationToken);

            // Update rate limits from response headers
            _rateLimits.UpdateFromHeaders(response);

            // Handle rate limiting
            if (RateLimits.IsRateLimited(response))
            {
                _rateLimits.ShowRateLimitError(response, console);
                throw new HttpRequestException($"Rate limited by Gemini API. Status: {response.StatusCode}");
            }

            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var geminiResponse = JsonConvert.DeserializeObject<GeminiResponse>(responseContent);

            if (geminiResponse?.Candidates == null || !geminiResponse.Candidates.Any())
            {
                throw new InvalidOperationException("No response candidates received from Gemini API");
            }

            var candidate = geminiResponse.Candidates.First();
            var responseText = candidate.Content?.Parts?.FirstOrDefault()?.Text ?? string.Empty;

            // Extract usage information if available
            var actualInputTokens = geminiResponse.UsageMetadata?.PromptTokenCount ?? inputTokens;
            var actualOutputTokens = geminiResponse.UsageMetadata?.CandidatesTokenCount ?? EstimateOutputTokens(responseText);

            // Update actual usage
            _rateLimits.UpdateActualUsage(actualInputTokens, actualOutputTokens);

            // Display usage info
            _rateLimits.DisplayUsageInfo(actualInputTokens, actualOutputTokens, console);
            _rateLimits.ShowStatus(console);

            // Extract thinking content if present
            var (cleanContent, thinkingContent) = ExtractThinkingContent(responseText);

            return new ProcessedResponse
            {
                Content = cleanContent,
                Thinking = thinkingContent,
                OutputTokensCount = actualOutputTokens
            };
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("Rate limited"))
        {
            console?.ShowError($"Gemini API rate limit exceeded: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            console?.ShowError($"Error calling Gemini API: {ex.Message}");
            throw;
        }
    }

    private GeminiRequest ConvertToGeminiRequest(List<CerebrasMessage> messages)
    {
        var request = new GeminiRequest();
        string? systemInstruction = null;

        foreach (var message in messages)
        {
            if (message.Role == "system")
            {
                // Extract system instruction
                systemInstruction = message.Content;
            }
            else
            {
                var role = message.Role == "assistant" ? "model" : message.Role;
                request.Contents.Add(new ContentItem
                {
                    Role = role,
                    Parts = new List<Part>
                    {
                        new Part { Text = message.Content }
                    }
                });
            }
        }

        // Add system instruction if present
        if (!string.IsNullOrEmpty(systemInstruction))
        {
            request.SystemInstruction = new ContentItem
            {
                Parts = new List<Part>
                {
                    new Part { Text = systemInstruction }
                }
            };
        }

        // Add generation config with thinking support
        request.GenerationConfig = new GenerationConfig
        {
            Temperature = 0.5f,
            TopP = 0.95f,
            MaxOutputTokens = 65536,
            ThinkingConfig = new ThinkingConfig
            {
                ThinkingBudget = -1,  // Dynamic thinking budget
                IncludeThoughts = true
            }
        };

        return request;
    }

    private int EstimateInputTokens(List<CerebrasMessage> messages)
    {
        // Rough estimation: 1 token ≈ 4 characters for English text
        var totalChars = messages.Sum(m => m.Content?.Length ?? 0);
        return (int)Math.Ceiling(totalChars / 4.0);
    }

    private (string cleanContent, string thinkingContent) ExtractThinkingContent(string fullResponse)
    {
        // Gemini 2.5 Flash uses <thinking> tags for thinking content
        const string thinkingStart = "<thinking>";
        const string thinkingEnd = "</thinking>";

        var thinkingStartIndex = fullResponse.IndexOf(thinkingStart);
        if (thinkingStartIndex == -1)
        {
            // No thinking content found
            return (fullResponse.Trim(), string.Empty);
        }

        var thinkingEndIndex = fullResponse.IndexOf(thinkingEnd, thinkingStartIndex);
        if (thinkingEndIndex == -1)
        {
            // Malformed thinking block, return original content
            return (fullResponse.Trim(), string.Empty);
        }

        // Extract thinking content (without the tags)
        var thinkingContent = fullResponse.Substring(
            thinkingStartIndex + thinkingStart.Length,
            thinkingEndIndex - thinkingStartIndex - thinkingStart.Length).Trim();

        // Extract clean content (everything except the thinking block)
        var beforeThinking = fullResponse.Substring(0, thinkingStartIndex);
        var afterThinking = fullResponse.Substring(thinkingEndIndex + thinkingEnd.Length);
        var cleanContent = (beforeThinking + afterThinking).Trim();

        return (cleanContent, thinkingContent);
    }

    private int EstimateOutputTokens(string text)
    {
        // Rough estimation: 1 token ≈ 4 characters for English text
        return (int)Math.Ceiling((text?.Length ?? 0) / 4.0);
    }

    #region Gemini Models
    private class GeminiRequest
    {
        [JsonProperty("contents")]
        public List<ContentItem> Contents { get; set; } = new List<ContentItem>();

        [JsonProperty("generationConfig", NullValueHandling = NullValueHandling.Ignore)]
        public GenerationConfig? GenerationConfig { get; set; }

        [JsonProperty("systemInstruction", NullValueHandling = NullValueHandling.Ignore)]
        public ContentItem? SystemInstruction { get; set; }
    }

    private class ContentItem
    {
        [JsonProperty("role", NullValueHandling = NullValueHandling.Ignore)]
        public string? Role { get; set; }

        [JsonProperty("parts")]
        public List<Part> Parts { get; set; } = new List<Part>();
    }

    private class Part
    {
        [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)]
        public string? Text { get; set; }
    }

    private class GenerationConfig
    {
        [JsonProperty("temperature", NullValueHandling = NullValueHandling.Ignore)]
        public float? Temperature { get; set; }

        [JsonProperty("topP", NullValueHandling = NullValueHandling.Ignore)]
        public float? TopP { get; set; }

        [JsonProperty("maxOutputTokens", NullValueHandling = NullValueHandling.Ignore)]
        public int? MaxOutputTokens { get; set; }

        [JsonProperty("thinkingConfig", NullValueHandling = NullValueHandling.Ignore)]
        public ThinkingConfig? ThinkingConfig { get; set; }
    }

    private class ThinkingConfig
    {
        [JsonProperty("thinkingBudget")]
        public int ThinkingBudget { get; set; }

        [JsonProperty("includeThoughts")]
        public bool IncludeThoughts { get; set; }
    }

    // Response models for parsing Gemini API response
    private class GeminiResponse
    {
        [JsonProperty("candidates")]
        public List<Candidate>? Candidates { get; set; }

        [JsonProperty("usageMetadata")]
        public UsageMetadata? UsageMetadata { get; set; }
    }

    private class Candidate
    {
        [JsonProperty("content")]
        public ContentItem? Content { get; set; }

        [JsonProperty("finishReason")]
        public string? FinishReason { get; set; }

        [JsonProperty("index")]
        public int Index { get; set; }
    }

    private class UsageMetadata
    {
        [JsonProperty("promptTokenCount")]
        public int PromptTokenCount { get; set; }

        [JsonProperty("candidatesTokenCount")]
        public int CandidatesTokenCount { get; set; }

        [JsonProperty("totalTokenCount")]
        public int TotalTokenCount { get; set; }
    }
    #endregion
}

