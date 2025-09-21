using System.Text.Json.Serialization;

namespace duo_code.Models;

/// <summary>
/// This is a simplified version that only contains what the API needs.
/// </summary>
public class RequestMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty; // "system", "user", "assistant"
    
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}