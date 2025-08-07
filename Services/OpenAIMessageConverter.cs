using duo_code.Models;

namespace duo_code.Services;

public class OpenAIMessageConvertor
{
    public static OpenAiRequest ConvertToOpenAiRequest(List<CerebrasMessage> messages, string model)
    {
        var request = new OpenAiRequest
        {
            Model = model,
            Messages = messages.Select(m => new OpenAiMessage
            {
                Role = ConvertRole(m.Role),
                Content = m.Content
            }).ToList(),
            Temperature = 0.7f,
            TopP = 1.0f,
            MaxTokens = 4096, // Limita specifică OpenAI, poți ajusta
            Stream = true
        };

        return request;
    }

    private static string ConvertRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return "user";
        
        return role.ToLower() switch
        {
            "system" => "system",
            "assistant" => "assistant",
            "user" => "user",
            _ => "user"
        };
    }
}