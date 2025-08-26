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
        private DateTimeOffset _windowStart = DateTimeOffset.UtcNow;
        
        public int InputTokensPerMinute { get; private set; } = 100_000;
        public int OutputTokensPerMinute { get; private set; } = 100_000;


        public RateLimits(int inputTokensPerMinute = 100_000, int outputTokensPerMinute = 100_000)
        {
            InputTokensPerMinute = inputTokensPerMinute;
            OutputTokensPerMinute = outputTokensPerMinute;
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
                
            return true;
        }

        public void RecordRequest(int inputTokens = 0, int outputTokens = 0)
        {
            CleanOldData();
            Interlocked.Add(ref _inputTokensUsed, inputTokens);
            Interlocked.Add(ref _outputTokensUsed, outputTokens);
        }

        public void UpdateActualUsage(int actualInputTokens, int actualOutputTokens)
        {
            CleanOldData();
            
            // Replace estimates with actual usage
            Interlocked.Exchange(ref _inputTokensUsed, actualInputTokens);
            Interlocked.Exchange(ref _outputTokensUsed, actualOutputTokens);
        }
        
        public void UpdateOpenAIActualUsage(int actualInputTokens, int actualOutputTokens)
        {
            CleanOldData();
            
            // For OpenAI: track individual tokens for display
            Interlocked.Exchange(ref _inputTokensUsed, actualInputTokens);
            Interlocked.Exchange(ref _outputTokensUsed, actualOutputTokens);
        }

        public void UpdateFromHeaders(HttpResponseMessage response)
        {
            var headers = response.Headers;
            
            // Input token limits
            int inputLimit;
            if (TryGetHeader(headers,
                "anthropic-ratelimit-input-tokens-limit",      // Anthropic
                "x-ratelimit-limit-input-tokens",              // OpenAI input specific
                "x-ratelimit-limit-prompt-tokens",             // OpenAI prompt tokens
                "x-ratelimit-limit-tokens",                    // OpenAI/others combined
                "x-ratelimit-limit-tokens-minute",             // Cerebras
                "x-ratelimit-limit-tpm",                       // OpenAI TPM
                "quota-input-tokens-minute",                   // Gemini input
                "quota-tokens-minute",                         // Gemini combined
                out inputLimit))
                InputTokensPerMinute = inputLimit;
                
            int inputRemaining;
            if (TryGetHeader(headers,
                "anthropic-ratelimit-input-tokens-remaining",  // Anthropic
                "x-ratelimit-remaining-input-tokens",          // OpenAI input specific
                "x-ratelimit-remaining-prompt-tokens",         // OpenAI prompt tokens
                "x-ratelimit-remaining-tokens",                // OpenAI/others combined
                "x-ratelimit-remaining-tokens-minute",         // Cerebras
                "x-ratelimit-remaining-tpm",                   // OpenAI TPM
                "quota-input-tokens-remaining",                // Gemini input
                "quota-tokens-remaining",                      // Gemini combined
                out inputRemaining))
            {
                var used = Math.Max(0, InputTokensPerMinute - inputRemaining);
                Interlocked.Exchange(ref _inputTokensUsed, used);
            }
            
            // Output token limits
            int outputLimit;
            if (TryGetHeader(headers,
                "anthropic-ratelimit-output-tokens-limit",     // Anthropic
                "x-ratelimit-limit-output-tokens",             // OpenAI output specific
                "x-ratelimit-limit-completion-tokens",         // OpenAI completion tokens
                "x-ratelimit-limit-tokens",                    // OpenAI/others combined fallback
                "x-ratelimit-limit-output-tokens-minute",      // Cerebras output
                "x-ratelimit-limit-tpm",                       // OpenAI TPM fallback
                "quota-output-tokens-minute",                  // Gemini output
                "quota-tokens-minute",                         // Gemini combined fallback
                out outputLimit))
                OutputTokensPerMinute = outputLimit;
                
            int outputRemaining;
            if (TryGetHeader(headers,
                "anthropic-ratelimit-output-tokens-remaining", // Anthropic
                "x-ratelimit-remaining-output-tokens",         // OpenAI output specific
                "x-ratelimit-remaining-completion-tokens",     // OpenAI completion tokens
                "x-ratelimit-remaining-tokens",                // OpenAI/others combined fallback
                "x-ratelimit-remaining-output-tokens-minute",  // Cerebras output
                "x-ratelimit-remaining-tpm",                   // OpenAI TPM fallback
                "quota-output-tokens-remaining",               // Gemini output
                "quota-tokens-remaining",                      // Gemini combined fallback
                out outputRemaining))
            {
                var used = Math.Max(0, OutputTokensPerMinute - outputRemaining);
                Interlocked.Exchange(ref _outputTokensUsed, used);
            }
            
            // Handle combined tokens for APIs that don't separate input/output
            int combinedLimit;
            if (TryGetHeader(headers,
                "anthropic-ratelimit-tokens-limit",            // Anthropic fallback
                "x-ratelimit-limit-tokens",                    // OpenAI/others combined
                "x-ratelimit-limit-tpm",                       // OpenAI TPM
                "quota-tokens-minute",                         // Gemini total
                out combinedLimit))
            {
                // For OpenAI, if we only have combined tokens and no separate input/output limits
                bool hasOpenAIHeaders = TryGetHeader(headers, "x-ratelimit-limit-tokens", out int _) || 
                                       TryGetHeader(headers, "x-ratelimit-limit-tpm", out int _);
                bool hasSeparateInputOutput = TryGetHeader(headers, "x-ratelimit-limit-input-tokens", out int _) || 
                                            TryGetHeader(headers, "x-ratelimit-limit-output-tokens", out int _);
                
                if (hasOpenAIHeaders && !hasSeparateInputOutput)
                {
                    // For OpenAI with combined limits, set both to the same value since they share the same pool
                    InputTokensPerMinute = combinedLimit;
                    OutputTokensPerMinute = combinedLimit;
                }
                else if (InputTokensPerMinute == 100_000) 
                {
                    InputTokensPerMinute = combinedLimit;
                }
                
                if (OutputTokensPerMinute == 100_000) 
                {
                    OutputTokensPerMinute = combinedLimit;
                }
            }
            
            int combinedRemaining;
            if (TryGetHeader(headers,
                "anthropic-ratelimit-tokens-remaining",        // Anthropic fallback  
                "x-ratelimit-remaining-tokens",                // OpenAI/others combined
                "x-ratelimit-remaining-tpm",                   // OpenAI TPM
                "quota-tokens-remaining",                      // Gemini total
                out combinedRemaining))
            {
                var used = Math.Max(0, (InputTokensPerMinute == OutputTokensPerMinute ? InputTokensPerMinute : Math.Max(InputTokensPerMinute, OutputTokensPerMinute)) - combinedRemaining);
                
                // For OpenAI with combined tokens, set both input and output to the same remaining value
                bool hasOpenAIHeaders = TryGetHeader(headers, "x-ratelimit-remaining-tokens", out int _) || 
                                       TryGetHeader(headers, "x-ratelimit-remaining-tpm", out int _);
                bool hasSeparateInputOutput = TryGetHeader(headers, "x-ratelimit-remaining-input-tokens", out int _) || 
                                            TryGetHeader(headers, "x-ratelimit-remaining-output-tokens", out int _);
                
                if (hasOpenAIHeaders && !hasSeparateInputOutput)
                {
                    // For OpenAI, both input and output track the same pool
                    Interlocked.Exchange(ref _inputTokensUsed, used);
                    Interlocked.Exchange(ref _outputTokensUsed, used);
                }
                else if (_inputTokensUsed == 0 && _outputTokensUsed == 0)
                {
                    // For other APIs, split evenly
                    Interlocked.Exchange(ref _inputTokensUsed, used / 2);
                    Interlocked.Exchange(ref _outputTokensUsed, used / 2);
                }
            }
        }
        
        public void CheckAndShowRateLimit(int inputTokens, int outputTokens, ConsoleInterface? console)
        {
            if (!CanMakeRequest(inputTokens, outputTokens))
            {
                WriteInfo("Rate limit would be exceeded with this request!");
                ShowStatus(console);
            }
        }
        
        public void ShowRateLimitError(HttpResponseMessage response, ConsoleInterface? console)
        {
            WriteInfo("Rate limit exceeded by the server!");
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
            WriteInfo($"Token Usage - Input: {inputTokens:N0}, Output: {outputTokens:N0}");
        }
        
        public void ShowCurrentRateLimits(ConsoleInterface? console)
        {
            WriteInfo("Current rate limits:");
            ShowStatus(console);
        }
        
        public void ShowNoUsageInfo(ConsoleInterface? console)
        {
            WriteInfo("No usage information available from API response.");
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
            
            var statusMessage = $"Input: {inputAvailable:N0}/{InputTokensPerMinute:N0} | Output: {outputAvailable:N0}/{OutputTokensPerMinute:N0}";
            
            if (console != null)
            {
                WriteInfo(statusMessage);
            }
            else
            {
                WriteInfo(statusMessage);
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

        private static bool TryGetHeader(System.Net.Http.Headers.HttpResponseHeaders headers, string headerName1, string headerName2, string headerName3, string headerName4, string headerName5, string headerName6, string headerName7, string headerName8, out int value)
        {
            value = 0;
            string[] names = { headerName1, headerName2, headerName3, headerName4, headerName5, headerName6, headerName7, headerName8 };
            
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