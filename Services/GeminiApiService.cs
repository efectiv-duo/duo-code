using System.Text;
using System.Text.Json;
using duo_code.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace duo_code.Services;

public class GeminiApiService : IApiService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:streamGenerateContent";
    
    public GeminiApiService(string apiKey)
    {
        _apiKey = apiKey;
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(5);
    }
    
    public async Task<ProcessedResponse> GetAISuggestionAsync(List<CerebrasMessage> messages, CancellationToken cancellationToken = default, string? model = null)
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
        
        return await StreamingResponseProcessor.ProcessAsync(response, cancellationToken, ApiProvider.Gemini);
    }
    
    private GeminiRequest ConvertToGeminiRequest(List<CerebrasMessage> messages)
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
        
        // Add generation config
        request.GenerationConfig = new GenerationConfig
        {
            Temperature = 0.5f,
            TopP = 0.95f,
            MaxOutputTokens = 8192
        };
        
        return request;
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
    }
    #endregion
}