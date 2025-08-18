namespace duo_code.Models;

public class ProcessedResponse
{
    public string Content { get; set; } = string.Empty; // Clean response without thinking blocks
    public string Thinking { get; set; } = string.Empty; // Extracted thinking content
    public int OutputTokens { get; set; } = 0;

    /// <summary>
    /// Gets the full response including thinking blocks
    /// </summary>
    public bool HasTokenUsage => OutputTokens > 0;

    public string GetTokenUsageInfo()
    {
        if (!HasTokenUsage)
            return "Token usage information not available";

        return $"Output-tokens: {OutputTokens}";
    }
}