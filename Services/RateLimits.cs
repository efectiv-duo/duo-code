using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading;
using System.Linq;
using duo_code.Models; 

namespace duo_code.Services
{
    public class RateLimits
    {
        private int _inputTokensUsed = 0;
        private int _outputTokensUsed = 0;
        private int _totalTokensUsed = 0;
        private DateTimeOffset _windowStart = DateTimeOffset.UtcNow;
        
        public int InputTokensPerMinute { get; private set; } = 100_000;
        public int OutputTokensPerMinute { get; private set; } = 100_000;
        public int TotalTokensPerMinute { get; private set; } = 200_000;

        public RateLimits(int inputTokensPerMinute = 100_000, int outputTokensPerMinute = 100_000)
        {
            InputTokensPerMinute = inputTokensPerMinute;
            OutputTokensPerMinute = outputTokensPerMinute;
            TotalTokensPerMinute = inputTokensPerMinute + outputTokensPerMinute;
        }

        public bool CanMakeRequest(int inputTokens = 0, int outputTokens = 0)
        {
            CleanOldData();
            
            // Check input token limit
            if (inputTokens > 0 && _inputTokensUsed + inputTokens > InputTokensPerMinute)
                return false;
                
            // Check output token limit  
            if (outputTokens > 0 && _outputTokensUsed + outputTokens > OutputTokensPerMinute)
                return false;
                
            // Check total token limit (for APIs that only have combined limits)
            var totalTokens = inputTokens + outputTokens;
            if (totalTokens > 0 && _totalTokensUsed + totalTokens > TotalTokensPerMinute)
                return false;
                
            return true;
        }

        public void RecordRequest(int inputTokens = 0, int outputTokens = 0)
        {
            CleanOldData();
            Interlocked.Add(ref _inputTokensUsed, inputTokens);
            Interlocked.Add(ref _outputTokensUsed, outputTokens);
            Interlocked.Add(ref _totalTokensUsed, inputTokens + outputTokens);
        }

        public void UpdateActualUsage(int actualInputTokens, int actualOutputTokens)
        {
            CleanOldData();
        }

        public void UpdateFromHeaders(HttpResponseMessage response)
        {
            var headers = response.Headers;
            
            // Input token limits
            int inputLimit;
            if (TryGetHeader(headers,
                "anthropic-ratelimit-input-tokens-limit",      // Anthropic
                "x-ratelimit-limit-tokens",                    // OpenAI (combined)
                "x-ratelimit-limit-tokens-minute",             // Cerebras
                "quota-tokens-minute", out inputLimit))
                InputTokensPerMinute = inputLimit;
                
            int inputRemaining;
            if (TryGetHeader(headers,
                "anthropic-ratelimit-input-tokens-remaining",  // Anthropic
                "x-ratelimit-remaining-tokens",                // OpenAI (combined) 
                "x-ratelimit-remaining-tokens-minute",         // Cerebras
                "quota-tokens-remaining", out inputRemaining))
            {
                var used = Math.Max(0, InputTokensPerMinute - inputRemaining);
                Interlocked.Exchange(ref _inputTokensUsed, used);
            }
            
            // Output token limits (mainly Anthropic)
            int outputLimit;
            if (TryGetHeader(headers,
                "anthropic-ratelimit-output-tokens-limit", out outputLimit))
                OutputTokensPerMinute = outputLimit;
                
            int outputRemaining;
            if (TryGetHeader(headers,
                "anthropic-ratelimit-output-tokens-remaining", out outputRemaining))
            {
                var used = Math.Max(0, OutputTokensPerMinute - outputRemaining);
                Interlocked.Exchange(ref _outputTokensUsed, used);
            }
            
            // Total tokens (for APIs without separate input/output)
            int totalLimit;
            if (TryGetHeader(headers,
                "anthropic-ratelimit-tokens-limit",            // Anthropic fallback
                "x-ratelimit-limit-tokens", out totalLimit))
            {
                TotalTokensPerMinute = totalLimit;
                // If no separate input/output limits, use total for both
                if (InputTokensPerMinute == 100_000) InputTokensPerMinute = totalLimit;
                if (OutputTokensPerMinute == 100_000) OutputTokensPerMinute = totalLimit;
            }
            
            int totalRemaining;
            if (TryGetHeader(headers,
                "anthropic-ratelimit-tokens-remaining",        // Anthropic fallback  
                "x-ratelimit-remaining-tokens", out totalRemaining))
            {
                var used = Math.Max(0, TotalTokensPerMinute - totalRemaining);
                Interlocked.Exchange(ref _totalTokensUsed, used);
                // Update individual counters if no separate tracking
                if (_inputTokensUsed == 0 && _outputTokensUsed == 0)
                {
                    Interlocked.Exchange(ref _inputTokensUsed, used / 2);
                    Interlocked.Exchange(ref _outputTokensUsed, used / 2);
                }
            }
        }
        public void CheckAndShowRateLimit(int inputTokens, int outputTokens, ConsoleInterface? console)
        {
            if (!CanMakeRequest(inputTokens, outputTokens))
            {
                console?.ShowError("Rate limit would be exceeded with this request!");
                ShowStatus(console);
            }
        }
        public void ShowRateLimitError(HttpResponseMessage response, ConsoleInterface? console)
        {
            console?.ShowError("Rate limit exceeded by the server!");
            ShowStatus(console);

            var retryAfter = GetRetryAfter(response);
            var errorMessage = $"Rate limit exceeded";
            if (retryAfter.HasValue)
            {
                errorMessage += "$. Retry after {retryAfter.Value} seconds";
            }

            console?.ShowError(errorMessage);
        }

        public void DisplayUsageInfo(int inputTokens, int outputTokens, ConsoleInterface? console)
        {
            console?.ShowInfo($"Token Usage - Input: {inputTokens:N0}, Output: {outputTokens:N0}");
        }
        public void ShowCurrentRateLimits(ConsoleInterface? console)
        {
            console?.ShowInfo("Current rate limits:");
            ShowStatus(console);
        }
        public void ShowNoUsageInfo(ConsoleInterface? console)
        {
            console?.ShowInfo("No usage information available from API response.");
        }

        public void ShowStatus()
        {
            ShowStatus(null);
        }

        public void ShowStatus(ConsoleInterface? console)
        {
            CleanOldData();
            var inputAvailable = Math.Max(0, InputTokensPerMinute - _inputTokensUsed);
            var outputAvailable = Math.Max(0, OutputTokensPerMinute - _outputTokensUsed);
            var totalAvailable = Math.Max(0, TotalTokensPerMinute - _totalTokensUsed);
            
            var statusMessage = $"Input: {inputAvailable:N0}/{InputTokensPerMinute:N0} | Output: {outputAvailable:N0}/{OutputTokensPerMinute:N0} | Total: {totalAvailable:N0}/{TotalTokensPerMinute:N0}";
            
            if (console != null)
            {
                console.ShowInfo(statusMessage);
            }
            else
            {
                Console.WriteLine(statusMessage);
            }
        }
        public string GetRateLimitSummary()
        {
            CleanOldData();
            
            var inputRemaining = Math.Max(0, InputTokensPerMinute - _inputTokensUsed);
            var outputRemaining = Math.Max(0, OutputTokensPerMinute - _outputTokensUsed);
            
            var inputPercent = InputTokensPerMinute > 0 ? 
                (double)_inputTokensUsed / InputTokensPerMinute * 100 : 0;
            var outputPercent = OutputTokensPerMinute > 0 ? 
                (double)_outputTokensUsed / OutputTokensPerMinute * 100 : 0;
                
            return $"Input: {inputRemaining:N0}/{InputTokensPerMinute:N0} ({inputPercent:F1}% used) | " +
                   $"Output: {outputRemaining:N0}/{OutputTokensPerMinute:N0} ({outputPercent:F1}% used)";
        }

        public static bool IsRateLimited(HttpResponseMessage response)
        {
            return response.StatusCode == System.Net.HttpStatusCode.TooManyRequests;
        }

        public static int? GetRetryAfter(HttpResponseMessage response)
        {
            if (response.Headers.TryGetValues("retry-after", out var values))
            {
                if (int.TryParse(string.Join("", values), out int seconds))
                    return seconds;
            }
            return null;
        }

        private void CleanOldData()
        {
            var cutoff = DateTimeOffset.UtcNow.AddMinutes(-1);
            
            // Reset if window expired
            if (_windowStart < cutoff)
            {
                Interlocked.Exchange(ref _inputTokensUsed, 0);
                Interlocked.Exchange(ref _outputTokensUsed, 0);
                Interlocked.Exchange(ref _totalTokensUsed, 0);
                _windowStart = DateTimeOffset.UtcNow;
            }
        }

        private static bool TryGetHeader(System.Net.Http.Headers.HttpResponseHeaders headers, params string[] headerNames)
        {
            foreach (var name in headerNames)
            {
                if (headers.TryGetValues(name, out var values))
                {
                    if (int.TryParse(string.Join("", values), out _))
                        return true;
                }
            }
            return false;
        }

        private static bool TryGetHeader(System.Net.Http.Headers.HttpResponseHeaders headers, string headerName, out int value)
        {
            value = 0;
            if (headers.TryGetValues(headerName, out var values))
            {
                if (int.TryParse(string.Join("", values), out value))
                    return true;
            }
            return false;
        }

        private static bool TryGetHeader(System.Net.Http.Headers.HttpResponseHeaders headers, string headerName1, string headerName2, out int value)
        {
            value = 0;
            string[] names = { headerName1, headerName2 };
            
            foreach (var name in names)
            {
                if (!string.IsNullOrEmpty(name) && headers.TryGetValues(name, out var values))
                {
                    if (int.TryParse(string.Join("", values), out value))
                        return true;
                }
            }
            return false;
        }

        private static bool TryGetHeader(System.Net.Http.Headers.HttpResponseHeaders headers, string headerName1, string headerName2, string headerName3, string headerName4, out int value)
        {
            value = 0;
            string[] names = { headerName1, headerName2, headerName3, headerName4 };
            
            foreach (var name in names)
            {
                if (!string.IsNullOrEmpty(name) && headers.TryGetValues(name, out var values))
                {
                    if (int.TryParse(string.Join("", values), out value))
                        return true;
                }
            }
            return false;
        }
    }
    public static class RateLimiters
    {
        private static readonly ConcurrentDictionary<string, RateLimits> _limiters = new();

        public static RateLimits Get(string key, int inputTokensPerMinute = 100_000, int outputTokensPerMinute = 100_000)
        {
            return _limiters.GetOrAdd(key, _ => new RateLimits(inputTokensPerMinute, outputTokensPerMinute));
        }
    }
}