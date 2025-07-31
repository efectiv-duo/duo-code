using duo_code.Models;
using duo_code.Services.Interfaces;

namespace duo_code.Services;

public interface IApiService
{
    Task<ProcessedResponse> GetAISuggestionAsync(List<CerebrasMessage> messages, CancellationToken cancellationToken = default, string? model = null, IConsoleInterface? console = null);
    void Dispose();
}