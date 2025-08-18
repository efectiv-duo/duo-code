using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace duo_code.Services;

public class OpenAiApiService : IApiService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl = "https://api.openai.com/v1/chat/completions";

    public OpenAiApiService(string apiKey)
    {
        _apiKey = apiKey;
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(5);

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
    }

    public async Task<ProcessedResponse> GetAISuggestionAsync(
        List<CerebrasMessage> messages,
        CancellationToken cancellationToken = default,
        string? model = null,
        ConsoleInterface? console = null)
    {
        var usedModel = model ?? CurrentState.Model;

        var request = OpenAIMessageConvertor.ConvertToOpenAiRequest(messages, usedModel);

        var jsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore
        };

        var requestJson = JsonConvert.SerializeObject(request, jsonSettings);
        using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");


        var response = await _httpClient.PostAsync(_baseUrl, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            console?.ShowError($"OpenAI API error: {response.StatusCode}\n{errorContent}");
            response.EnsureSuccessStatusCode(); // va arunca exceptie
        }

        var responseJson = await response.Content.ReadAsStringAsync();

        var openAIResponse = JsonConvert.DeserializeObject<OpenAIResponse>(responseJson);

        var contentText = openAIResponse?.Choices?.FirstOrDefault()?.Message?.Content ?? "";
        var tokenCount = openAIResponse?.Usage?.CompletionTokens ?? 0;

        WriteInfo($"Output: {tokenCount} tokens.");

        return new ProcessedResponse
        {
            Content = contentText,
            TokenCount = tokenCount
        };
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}