namespace duo_code.Models;

public class ProcessedResponse
{
    public string Content { get; set; } = string.Empty; // Clean response without thinking blocks
    public string Thinking { get; set; } = string.Empty; // Extracted thinking content

    /// <summary>
    /// Gets the full response including thinking blocks
    /// </summary>
    public string ContentWithThinking =>
        Thinking.Length > 0 ? $"<think>{Thinking}</think>{Content}" : Content;
        
    public int TokenCount { get; set; } = 0;
}