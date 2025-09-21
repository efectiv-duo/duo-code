using duo_code.Models;

namespace duo_code.Services;

public interface IApiService
{
    Task<ProcessedResponse> GetAISuggestionAsync(List<RequestMessage> messages, CancellationToken cancellationToken = default, string? model = null, ConsoleInterface? console = null);
    void Dispose();
}