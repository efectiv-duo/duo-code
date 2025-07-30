using duo_code.Models;

namespace duo_code.Services;

public interface IApiService
{
    Task<ProcessedResponse> GetAISuggestionAsync(List<CerebrasMessage> messages, CancellationToken cancellationToken = default, string? model = null, SpectreConsoleInterface? console = null);
    void Dispose();
}