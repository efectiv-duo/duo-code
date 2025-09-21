using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using duo_code.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace duo_code.Services;

public class GeminiApiService : IApiService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";
    
    public GeminiApiService(string apiKey)
    {
        _apiKey = apiKey;
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(5);
    }

    public async Task<ProcessedResponse> GetAISuggestionAsync(List<RequestMessage> messages, CancellationToken cancellationToken = default, string? model = null, ConsoleInterface? console = null)
    {
        var apiUrl = $"{_baseUrl}?key={_apiKey}";

        var request = ConvertToGeminiRequest(messages);

        var jsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore
        };

        var requestJson = JsonConvert.SerializeObject(request, jsonSettings);
        var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(apiUrl, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseGeminiResponse(body);
    }
    
    private GeminiRequest ConvertToGeminiRequest(List<RequestMessage> messages)
    {
        var request = new GeminiRequest();
        string? systemInstruction = null;
        
        foreach (var message in messages)
        {
            if (message.Role == "system")
            {
                // Extract system instruction
                systemInstruction = message.Content;
            }
            else
            {
                var role = message.Role == "assistant" ? "model" : message.Role;
                request.Contents.Add(new ContentItem
                {
                    Role = role,
                    Parts = new List<Part>
                    {
                        new Part { Text = message.Content }
                    }
                });
            }
        }
        
        // Add system instruction if present
        if (!string.IsNullOrEmpty(systemInstruction))
        {
            request.SystemInstruction = new ContentItem
            {
                Parts = new List<Part>
                {
                    new Part { Text = systemInstruction }
                }
            };
        }
        
        // Add generation config with thinking support
        request.GenerationConfig = new GenerationConfig
        {
            Temperature = 0.5f,
            TopP = 0.95f,
            MaxOutputTokens = 65536,
            ThinkingConfig = new ThinkingConfig
            {
                ThinkingBudget = -1,  // Dynamic thinking budget
                IncludeThoughts = true
            }
        };
        
        return request;
    }

    private static ProcessedResponse ParseGeminiResponse(string json)
    {
        var n = JsonNode.Parse(json);
        var parts = n?["candidates"]?[0]?["content"]?["parts"] as JsonArray;

        string content = string.Empty;
        string thinking = string.Empty;

        if (parts is { Count: > 0 })
        {
            // last part -> content
            content = parts[^1]?["text"]?.GetValue<string>() ?? string.Empty;

            // earlier parts with thought:true -> thinking
            var sb = new StringBuilder();
            for (int i = 0; i < parts.Count - 1; i++)
            {
                var p = parts[i];
                if (p?["thought"]?.GetValue<bool>() == true)
                {
                    var t = p?["text"]?.GetValue<string>();
                    if (!string.IsNullOrEmpty(t))
                    {
                        if (sb.Length > 0) sb.AppendLine().AppendLine();
                        sb.Append(t);
                    }
                }
            }
            thinking = sb.ToString();
        }

        int outputTokens = n?["usageMetadata"]?["totalTokenCount"]?.GetValue<int?>() ?? 0;

        return new ProcessedResponse
        {
            Content = content,
            Thinking = thinking,
            OutputTokensCount = outputTokens
        };
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }

    #region Gemini Models
    private class GeminiRequest
    {
        [JsonProperty("contents")]
        public List<ContentItem> Contents { get; set; } = new List<ContentItem>();

        [JsonProperty("generationConfig", NullValueHandling = NullValueHandling.Ignore)]
        public GenerationConfig? GenerationConfig { get; set; }

        [JsonProperty("systemInstruction", NullValueHandling = NullValueHandling.Ignore)]
        public ContentItem? SystemInstruction { get; set; }
    }

    private class ContentItem
    {
        [JsonProperty("role", NullValueHandling = NullValueHandling.Ignore)]
        public string? Role { get; set; }

        [JsonProperty("parts")]
        public List<Part> Parts { get; set; } = new List<Part>();
    }

    private class Part
    {
        [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)]
        public string? Text { get; set; }
    }

    private class GenerationConfig
    {
        [JsonProperty("temperature", NullValueHandling = NullValueHandling.Ignore)]
        public float? Temperature { get; set; }
        
        [JsonProperty("topP", NullValueHandling = NullValueHandling.Ignore)]
        public float? TopP { get; set; }
        
        [JsonProperty("maxOutputTokens", NullValueHandling = NullValueHandling.Ignore)]
        public int? MaxOutputTokens { get; set; }
        
        [JsonProperty("thinkingConfig", NullValueHandling = NullValueHandling.Ignore)]
        public ThinkingConfig? ThinkingConfig { get; set; }
    }
    
    private class ThinkingConfig
    {
        [JsonProperty("thinkingBudget")]
        public int ThinkingBudget { get; set; }
        
        [JsonProperty("includeThoughts")]
        public bool IncludeThoughts { get; set; }
    }
    #endregion
}