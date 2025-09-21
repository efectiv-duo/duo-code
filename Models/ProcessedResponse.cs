namespace duo_code.Models;

public class ProcessedResponse
{
    public string Content { get; set; } = string.Empty; // Clean response without thinking blocks
    public string Thinking { get; set; } = string.Empty; // Extracted thinking content
    public int OutputTokensCount { get; set; } = 0;
}