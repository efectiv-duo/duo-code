using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace duo_code.Services;

public class OpenAiApiService : IApiService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl = "https://api.openai.com/v1/chat/completions";

    private const int DefaultTpm = 60_000;

    public OpenAiApiService(string apiKey)
    {
        _apiKey = apiKey;
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(5);

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
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

        var jsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore
        };

        var requestJson = JsonConvert.SerializeObject(request, jsonSettings);
        using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        var estInput = EstimateInputTokens(messages);
        var reserve = Math.Max(estInput, request.MaxTokens ?? estInput);
        var partitionKey = BuildPartitionKey(_apiKey, usedModel);
        var budget = TokenBudgets.Get(partitionKey, DefaultTpm);

        if (!budget.TryReserve(reserve))
        {
            var available = budget.AvailableNow;
            console?.ShowError($"Local token rate limit: need {reserve}, available {available}/{budget.TpmLimit} in the current window.");
            throw new HttpRequestException("Too Many Tokens (local TPM limit).", null, HttpStatusCode.TooManyRequests);
        }

        HttpResponseMessage? response = null;
        string? responseJson = null;
        int actualTotal = 0;

        try
        {
            response = await _httpClient.PostAsync(_baseUrl, content, cancellationToken);
            var (limitTokens, remainingTokens, resetDelay) = ReadRateHeaders(response);
            if (limitTokens is not null || remainingTokens is not null || resetDelay is not null)
            {
                budget.SyncFromHeaders(limitTokens, remainingTokens, resetDelay);
                WriteInfo($"[RateLimit] server TPM={limitTokens?.ToString() ?? "?"}, remaining={remainingTokens?.ToString() ?? "?"}, reset={resetDelay?.ToString() ?? "?"}");
            }

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == (HttpStatusCode)429)
                {
                    var delay = GetRetryAfter(response) ?? resetDelay ?? TimeSpan.FromSeconds(2);
                    console?.ShowError($"OpenAI 429: waiting {delay.TotalSeconds:0.#}s (per headers).");
                    await Task.Delay(delay, cancellationToken);
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                console?.ShowError($"OpenAI API error: {response.StatusCode}\n{errorContent}");
                response.EnsureSuccessStatusCode();
            }

            responseJson = await response.Content.ReadAsStringAsync();

            var openAIResponseTmp = JsonConvert.DeserializeObject<OpenAIResponse>(responseJson);
            var u = openAIResponseTmp?.Usage;
            actualTotal = u?.TotalTokens ?? ((u?.PromptTokens ?? 0) + (u?.CompletionTokens ?? 0));
        }
        finally
        {
            var refund = Math.Max(0, reserve - actualTotal);
            if (refund > 0) budget.Release(refund);
        }

        if (responseJson is null)
            throw new InvalidOperationException("Could not obtain a valid response from OpenAI.");

        var openAIResponse = JsonConvert.DeserializeObject<OpenAIResponse>(responseJson);

        var contentText = openAIResponse?.Choices?.FirstOrDefault()?.Message?.Content ?? "";
        var tokenCount = openAIResponse?.Usage?.CompletionTokens ?? 0;

        WriteInfo($"TPM={budget.TpmLimit} | input≈{estInput} + output={tokenCount} → total≈{actualTotal} tokens.");

        return new ProcessedResponse
        {
            Content = contentText,
            TokenCount = tokenCount
        };
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }

    private static string BuildPartitionKey(string apiKey, string model)
        => $"{model}:{Hash(apiKey)}";

    private static string Hash(string s)
        => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(s))).Substring(0, 12);

    /// Quick-and-dirty input token estimation (≈ 4 chars/token + small per-message overhead).
    private static int EstimateInputTokens(List<CerebrasMessage> messages)
    {
        var chars = messages?.Sum(m => m?.Content?.Length ?? 0) ?? 0;
        var approx = Math.Max(1, chars / 4);
        var overhead = (messages?.Count ?? 0) * 8;
        return approx + overhead;
    }

    // ADD: read rate-limit headers commonly sent by OpenAI/Azure OpenAI
    private static (int? limitTokens, int? remainingTokens, TimeSpan? resetDelay) ReadRateHeaders(HttpResponseMessage response)
    {
        int? limit = null, remaining = null;
        TimeSpan? reset = null;

        if (response.Headers.TryGetValues("x-ratelimit-limit-tokens", out var v1) && int.TryParse(v1.FirstOrDefault(), out var l))
            limit = l;

        if (response.Headers.TryGetValues("x-ratelimit-remaining-tokens", out var v2) && int.TryParse(v2.FirstOrDefault(), out var r))
            remaining = r;

        if (response.Headers.TryGetValues("x-ratelimit-reset-tokens", out var v3))
            reset = ParseDuration(v3.FirstOrDefault()); // e.g. "23s", "1m0s"

        // Fallback: sometimes only "requests" headers are present
        if (reset is null && response.Headers.TryGetValues("x-ratelimit-reset-requests", out var v4))
            reset = ParseDuration(v4.FirstOrDefault());

        return (limit, remaining, reset);
    }

    // ADD: respect Retry-After when present
    private static TimeSpan? GetRetryAfter(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("Retry-After", out var values))
        {
            var v = values.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(v)) return null;

            if (int.TryParse(v, out var sec)) return TimeSpan.FromSeconds(sec);

            if (DateTimeOffset.TryParse(v, out var when))
            {
                var delta = when - DateTimeOffset.UtcNow;
                if (delta > TimeSpan.Zero) return delta;
            }
        }
        return null;
    }

    // ADD: parse durations like "6m0s", "23s"
    private static TimeSpan? ParseDuration(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        try
        {
            var total = TimeSpan.Zero;
            var num = 0;
            foreach (var ch in s)
            {
                if (char.IsDigit(ch)) { num = num * 10 + (ch - '0'); continue; }
                if (ch == 'h') { total += TimeSpan.FromHours(num); num = 0; continue; }
                if (ch == 'm') { total += TimeSpan.FromMinutes(num); num = 0; continue; }
                if (ch == 's') { total += TimeSpan.FromSeconds(num); num = 0; continue; }
            }
            return total;
        }
        catch { return null; }
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + " …";
}