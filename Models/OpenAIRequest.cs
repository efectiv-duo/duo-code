using Newtonsoft.Json;

namespace duo_code.Models;

public class OpenAIRequest
{
    [JsonProperty("model")]
    public string Model { get; set; } = "";

    [JsonProperty("messages")]
    public List<OpenAiMessage> Messages { get; set; } = new List<OpenAiMessage>();

    [JsonProperty("temperature", NullValueHandling = NullValueHandling.Ignore)]
    public float? Temperature { get; set; }

    [JsonProperty("top_p", NullValueHandling = NullValueHandling.Ignore)]
    public float? TopP { get; set; }

    [JsonProperty("max_tokens", NullValueHandling = NullValueHandling.Ignore)]
    public int? MaxTokens { get; set; }

    [JsonProperty("stream")]
    public bool Stream { get; set; }
}

public class OpenAiMessage
{
    [JsonProperty("role")]
    public string Role { get; set; } = "";

    [JsonProperty("content")]
    public string Content { get; set; } = "";
}