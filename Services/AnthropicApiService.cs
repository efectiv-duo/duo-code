using System.Text;
using System.Text.Json;
using System.Net;
using duo_code.Commands.Actions;
using duo_code.Models;

namespace duo_code.Services;

public class AnthropicApiService : IApiService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiUrl = "https://api.anthropic.com/v1/messages";

    public AnthropicApiService(string apiKey)
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
    }

    public async Task<ProcessedResponse> GetAISuggestionAsync(
        List<CerebrasMessage> messages, 
        CancellationToken cancellationToken = default, 
        string? model = null, 
        ConsoleInterface? console = null)
    {
        var targetModel = model ?? GetAnthropicModel(CurrentState.Model);
        var request = ConvertToAnthropicRequest(messages, targetModel);
        
        var jsonOptions = new JsonSerializerOptions 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
        
        var requestJson = JsonSerializer.Serialize(request, jsonOptions);
        var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(_apiUrl, content, cancellationToken);
        
        response.EnsureSuccessStatusCode();
        return await StreamingResponseProcessor.ProcessAsync(response, cancellationToken, ApiProvider.Anthropic, console);
    }

    private object ConvertToAnthropicRequest(List<CerebrasMessage> messages, string model)
    {
        var (anthropicMessages, systemMessage) = ConvertToAnthropicFormat(messages);
        
        return new
        {
            model = model,
            max_tokens = 8192,
            temperature = 0.5,
            stream = true,
            messages = anthropicMessages,
            system = systemMessage
        };
    }

    private (List<object> messages, string? systemMessage) ConvertToAnthropicFormat(List<CerebrasMessage> messages)
    {
        var anthropicMessages = new List<object>();
        string? systemMessage = null;

        foreach (var message in messages)
        {
            if (message.Role == "system")
            {
                if (string.IsNullOrEmpty(systemMessage))
                {
                    systemMessage = message.Content;
                }
                else
                {
                    systemMessage += "\n\n" + message.Content;
                }
            }
            else if (message.Role == "user" || message.Role == "assistant")
            {
                anthropicMessages.Add(new
                {
                    role = message.Role,
                    content = message.Content
                });
            }
        }

        if (anthropicMessages.Count > 0)
        {
            var firstMessage = anthropicMessages[0] as dynamic;
            if (firstMessage?.role != "user")
            {
                anthropicMessages.Insert(0, new
                {
                    role = "user",
                    content = "Please continue with the task."
                });
            }
        }

        return (anthropicMessages, systemMessage);
    }

    private string GetAnthropicModel(string currentModel)
    {
        if (CurrentState.AvailableModels[ApiProvider.Anthropic].Contains(currentModel))
        {
            return currentModel;
        }
        
        return "claude-3-5-sonnet-20241022";
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}