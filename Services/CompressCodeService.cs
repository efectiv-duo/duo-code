using duo_code.Models;

namespace duo_code.Services;

public class CompressCodeService
{
    private readonly ApiService _apiService;
    private const string CompressionPrompt = @"You are an AI code analyzer. Your task is to distill source code into a highly condensed, machine-readable structural summary. The output must be in a minimal YAML format.

**Schema & Rules:**

1.  **Top-level keys:** `file_purpose`, `dependencies`, `definitions`.
2.  **`file_purpose`:** A one-sentence description of the file's role.
3.  **`dependencies`:** A list of imported modules.
4.  **`definitions`:** A list of all major components (classes, functions, etc.). Each component MUST include:
    *   `type:` (e.g., class, function)
    *   `name:`
    *   `signature:` (params and return type)
    *   `purpose:` (A very brief summary of its goal)
    *   `methods:` (For classes only, a list of its methods with their own `name`, `signature`, and `purpose`)

**CRITICAL:** Do not include the original code, implementation logic, comments, or any conversational text. The output must be pure, structured data representing the code's architecture.";
    
    public CompressCodeService(ApiService apiService)
    {
        _apiService = apiService;
    }
    
    /// <summary>
    /// Compresses code content using a lightweight model and smart prompt
    /// </summary>
    /// <param name="content">The code content to compress</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Compressed version of the code</returns>
    public async Task<string> CompressCodeAsync(string content, CancellationToken cancellationToken = default)
    {
        var messages = new List<CerebrasMessage>
        {
            new CerebrasMessage
            {
                Role = "system",
                Content = CompressionPrompt
            },
            new CerebrasMessage
            {
                Role = "user",
                Content = content
            }
        };
        
        // Use lightweight model for compression (qwen-3-32b)
        var response = await _apiService.GetAISuggestionAsync(messages, cancellationToken, "qwen-3-32b");
        return response.Content;
    }
    
    /// <summary>
    /// Compresses multiple code contents in parallel
    /// </summary>
    public async Task<Dictionary<string, string>> CompressMultipleAsync(
        Dictionary<string, string> contents, 
        CancellationToken cancellationToken = default)
    {
        var tasks = contents.Select(async kvp =>
        {
            var compressed = await CompressCodeAsync(kvp.Value, cancellationToken);
            return new KeyValuePair<string, string>(kvp.Key, compressed);
        });
        
        var results = await Task.WhenAll(tasks);
        return results.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }
}