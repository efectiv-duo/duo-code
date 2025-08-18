using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using duo_code.Models;

namespace duo_code.Services
{
    public class RateLimitInfo
    {
        // Input token limits
        public int? InputTokensLimit { get; set; }
        public int? InputTokensRemaining { get; set; }
        public DateTime? InputTokensReset { get; set; }

        // Output token limits
        public int? OutputTokensLimit { get; set; }
        public int? OutputTokensRemaining { get; set; }
        public DateTime? OutputTokensReset { get; set; }

        public int? RetryAfterSeconds { get; set; }

        public void DisplayRateLimitInfo()
        {
            Console.WriteLine("=== Rate Limit Information ===");
            
            // Input tokens
            if (InputTokensLimit.HasValue)
                Console.WriteLine($"Input Token Limit: {InputTokensLimit:N0}");
            if (InputTokensRemaining.HasValue)
                Console.WriteLine($"Input Tokens Remaining: {InputTokensRemaining:N0}");
            if (InputTokensReset.HasValue)
                Console.WriteLine($"Input Tokens Reset At: {InputTokensReset:yyyy-MM-dd HH:mm:ss UTC}");

            Console.WriteLine(); // Separator

            // Output tokens
            if (OutputTokensLimit.HasValue)
                Console.WriteLine($"Output Token Limit: {OutputTokensLimit:N0}");
            if (OutputTokensRemaining.HasValue)
                Console.WriteLine($"Output Tokens Remaining: {OutputTokensRemaining:N0}");
            if (OutputTokensReset.HasValue)
                Console.WriteLine($"Output Tokens Reset At: {OutputTokensReset:yyyy-MM-dd HH:mm:ss UTC}");

            if (RetryAfterSeconds.HasValue)
                Console.WriteLine($"⚠️  Retry After: {RetryAfterSeconds} seconds");
        }

        public bool IsNearInputTokenLimit(int threshold = 100)
        {
            return InputTokensRemaining.HasValue && InputTokensRemaining.Value <= threshold;
        }

        public bool IsNearOutputTokenLimit(int threshold = 100)
        {
            return OutputTokensRemaining.HasValue && OutputTokensRemaining.Value <= threshold;
        }

        public bool IsNearTokenLimit(int threshold = 100)
        {
            return IsNearInputTokenLimit(threshold) || IsNearOutputTokenLimit(threshold);
        }

        public void ShowLimitWarnings(ConsoleInterface? console, int tokenThreshold = 1000)
        {
            if (console == null) return;

            if (IsNearInputTokenLimit(tokenThreshold))
            {
                console.ShowError("⚠️  Warning: Approaching input token limit!");
            }

            if (IsNearOutputTokenLimit(tokenThreshold))
            {
                console.ShowError("⚠️  Warning: Approaching output token limit!");
            }
        }

        public double? GetInputTokenUsagePercentage()
        {
            if (!InputTokensLimit.HasValue || !InputTokensRemaining.HasValue)
                return null;

            var used = InputTokensLimit.Value - InputTokensRemaining.Value;
            return (double)used / InputTokensLimit.Value * 100;
        }

        public double? GetOutputTokenUsagePercentage()
        {
            if (!OutputTokensLimit.HasValue || !OutputTokensRemaining.HasValue)
                return null;

            var used = OutputTokensLimit.Value - OutputTokensRemaining.Value;
            return (double)used / OutputTokensLimit.Value * 100;
        }

        public string GetRateLimitSummary()
        {
            var summary = new List<string>();

            if (InputTokensRemaining.HasValue && InputTokensLimit.HasValue)
            {
                var inputPercent = GetInputTokenUsagePercentage();
                summary.Add($"Input: {InputTokensRemaining:N0}/{InputTokensLimit:N0} ({inputPercent:F1}% used)");
            }

            if (OutputTokensRemaining.HasValue && OutputTokensLimit.HasValue)
            {
                var outputPercent = GetOutputTokenUsagePercentage();
                summary.Add($"Output: {OutputTokensRemaining:N0}/{OutputTokensLimit:N0} ({outputPercent:F1}% used)");
            }

            if (RetryAfterSeconds.HasValue)
            {
                summary.Add($"Retry after: {RetryAfterSeconds} seconds");
            }

            return summary.Any() ? string.Join(" | ", summary) : "No rate limit data available";
        }

        public int? GetWaitTimeSeconds()
        {
            return RetryAfterSeconds;
        }

        public bool IsRateLimited()
        {
            return RetryAfterSeconds.HasValue;
        }
    }

    public static class AnthropicRateLimits
    {
        public static RateLimitInfo ParseRateLimitHeaders(HttpResponseMessage response)
        {
            if (response == null) return new RateLimitInfo();

            var rateLimitInfo = new RateLimitInfo();

            // Parse input token limits
            if (response.Headers.TryGetValues("anthropic-ratelimit-input-tokens-limit", out var inputTokensLimitValues))
            {
                if (int.TryParse(string.Join("", inputTokensLimitValues), out int inputTokensLimit))
                    rateLimitInfo.InputTokensLimit = inputTokensLimit;
            }

            if (response.Headers.TryGetValues("anthropic-ratelimit-input-tokens-remaining", out var inputTokensRemainingValues))
            {
                if (int.TryParse(string.Join("", inputTokensRemainingValues), out int inputTokensRemaining))
                    rateLimitInfo.InputTokensRemaining = inputTokensRemaining;
            }

            if (response.Headers.TryGetValues("anthropic-ratelimit-input-tokens-reset", out var inputTokensResetValues))
            {
                if (DateTime.TryParse(string.Join("", inputTokensResetValues), out DateTime inputTokensReset))
                    rateLimitInfo.InputTokensReset = inputTokensReset.ToUniversalTime();
            }

            // Parse output token limits
            if (response.Headers.TryGetValues("anthropic-ratelimit-output-tokens-limit", out var outputTokensLimitValues))
            {
                if (int.TryParse(string.Join("", outputTokensLimitValues), out int outputTokensLimit))
                    rateLimitInfo.OutputTokensLimit = outputTokensLimit;
            }

            if (response.Headers.TryGetValues("anthropic-ratelimit-output-tokens-remaining", out var outputTokensRemainingValues))
            {
                if (int.TryParse(string.Join("", outputTokensRemainingValues), out int outputTokensRemaining))
                    rateLimitInfo.OutputTokensRemaining = outputTokensRemaining;
            }

            if (response.Headers.TryGetValues("anthropic-ratelimit-output-tokens-reset", out var outputTokensResetValues))
            {
                if (DateTime.TryParse(string.Join("", outputTokensResetValues), out DateTime outputTokensReset))
                    rateLimitInfo.OutputTokensReset = outputTokensReset.ToUniversalTime();
            }

            // Fallback: if separate headers don't exist, try the original combined headers
            if (!rateLimitInfo.InputTokensLimit.HasValue && !rateLimitInfo.OutputTokensLimit.HasValue)
            {
                if (response.Headers.TryGetValues("anthropic-ratelimit-tokens-limit", out var tokensLimitValues))
                {
                    if (int.TryParse(string.Join("", tokensLimitValues), out int tokensLimit))
                    {
                        // If only combined limit is available, treat it as input limit for backward compatibility
                        rateLimitInfo.InputTokensLimit = tokensLimit;
                    }
                }

                if (response.Headers.TryGetValues("anthropic-ratelimit-tokens-remaining", out var tokensRemainingValues))
                {
                    if (int.TryParse(string.Join("", tokensRemainingValues), out int tokensRemaining))
                        rateLimitInfo.InputTokensRemaining = tokensRemaining;
                }

                if (response.Headers.TryGetValues("anthropic-ratelimit-tokens-reset", out var tokensResetValues))
                {
                    if (DateTime.TryParse(string.Join("", tokensResetValues), out DateTime tokensReset))
                        rateLimitInfo.InputTokensReset = tokensReset.ToUniversalTime();
                }
            }

            if (response.Headers.TryGetValues("retry-after", out var retryAfterValues))
            {
                if (int.TryParse(string.Join("", retryAfterValues), out int retryAfter))
                    rateLimitInfo.RetryAfterSeconds = retryAfter;
            }

            return rateLimitInfo;
        }

        public static RateLimitInfo ParseAndExtractRateLimitHeaders(HttpResponseMessage response, AnthropicUsage? usage = null)
        {
            var rateLimitInfo = ParseRateLimitHeaders(response);
            return rateLimitInfo;
        }

        public static (AnthropicUsage? usage, RateLimitInfo rateLimits) ExtractCompleteUsageInfo(HttpResponseMessage response, AnthropicUsage? usage)
        {
            var rateLimitInfo = ParseRateLimitHeaders(response);
            return (usage, rateLimitInfo);
        }

        public static void DisplayCompleteUsageInfo(AnthropicUsage? usage, RateLimitInfo rateLimits, ConsoleInterface? console = null)
        {
            if (usage != null)
            {
                Console.WriteLine($"📊 Current Request - Input: {usage.InputTokens} tokens | Output: {usage.OutputTokens} tokens");
            }

            rateLimits.DisplayRateLimitInfo();
            rateLimits.ShowLimitWarnings(console);
        }

        public static bool IsRateLimitedResponse(HttpResponseMessage response)
        {
            return response.StatusCode == System.Net.HttpStatusCode.TooManyRequests;
        }
    }
}