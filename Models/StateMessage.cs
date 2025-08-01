using duo_code.Services;
using duo_code.Tools.Core;

namespace duo_code.Models;

/// <summary>
/// Internal message representation for session history.
/// Contains additional metadata and tracking information not sent to the API.
/// </summary>
public class StateMessage
{
    public string? Role { get; set; } // "system", "user", "assistant"
    public string Content { get; set; } = string.Empty; // Clean response without thinking blocks
    public string Thinking { get; set; } = string.Empty; // Extracted thinking content

    /// <summary>
    /// Gets the full response including thinking blocks
    /// </summary>
    public string ContentWithThinking =>
        Thinking.Length > 0 ? $"<think>{Thinking}</think>{Content}" : Content;

    // Parsed actions from assistant messages
    public List<IToolAction>? Actions { get; set; }

    public string BuildToolResultsMessage(bool forSummary = false)
    {
        if (Actions == null || Actions.Count == 0)
            return Content;

        var results = Actions
            .Where(action => action.FullResult != null)
            .Select(action => FormatToolResult(action, forSummary));

        var output = string.Join("\n\n", results);

        // Read screen DOM
        var doc = ScreenReaderService.ReadScreen();

        return output + $"\n\n Screen: {doc.ToString()}";
    }

    private string FormatToolResult(IToolAction action, bool forSummary)
    {
        var resultText = forSummary && action.SummarizedResult != null
            ? action.SummarizedResult
            : action.FullResult;

        return $"{resultText}";
    }

    /// <summary>
    /// Gets the content for chat history based on context parameters
    /// </summary>
    /// <param name="distanceToHead">Distance from the most recent message (0 = most recent)</param>
    /// <param name="historyTotalLength">Total number of messages in history</param>
    /// <returns>Appropriately formatted content for the chat history</returns>
    public string GetContentForHistory(int distanceToHead, int historyTotalLength)
    {
        return Role switch
        {
            "system" => Content,
            "user" => GetUserMessageContent(distanceToHead, historyTotalLength),
            "assistant" => GetAssistantMessageContent(distanceToHead, historyTotalLength),
            _ => Content
        };
    }

    private string GetUserMessageContent(int distanceToHead, int historyTotalLength)
    {
        // if (Actions == null || Actions.Count == 0)
        return Content;

        var shouldUseSummary = CalculateHistoricalDistance(distanceToHead, historyTotalLength) > 0.75;
        return shouldUseSummary ? BuildToolResultsMessage(true) : Content;
    }

    private string GetAssistantMessageContent(int distanceToHead, int historyTotalLength)
    {
        // For the most recent assistant message, include thinking
        // if (distanceToHead == 0 && Thinking.Length > 0)
        return ContentWithThinking;

        return TruncateContent(distanceToHead, historyTotalLength);
    }

    private string TruncateContent(int distanceToHead, int historyTotalLength)
    {
        var maxLines = GetMaxLinesForDistance(distanceToHead, historyTotalLength);
        var lines = Content.Split('\n');

        if (lines.Length <= maxLines)
            return Content;

        var truncatedLines = lines.Take(maxLines).ToArray();
        var remainingLines = lines.Length - maxLines;
        return $"{string.Join('\n', truncatedLines)}\n... + {remainingLines} lines";
    }

    private int GetMaxLinesForDistance(int distanceToHead, int historyTotalLength)
    {
        var distance = CalculateHistoricalDistance(distanceToHead, historyTotalLength);

        return distance switch
        {
            < 0.25f => 15,  // Recent messages
            <= 0.5f => 10,  // Medium-age messages
            _ => 5          // Old messages
        };
    }

    private float CalculateHistoricalDistance(int distanceToHead, int historyTotalLength)
    {
        return historyTotalLength > 0 ? (float)distanceToHead / historyTotalLength : 0;
    }
}