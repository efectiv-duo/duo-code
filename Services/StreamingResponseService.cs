using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using duo_code.Models;

namespace duo_code.Services
{
    public class StreamingResponseService
    {
        public async Task<ProcessedResponse> ProcessStreamingResponseAsync(
            Task<HttpResponseMessage> responseTask,
            CancellationToken cancellationToken)
        {
            var response = await responseTask;
            return await StreamingResponseProcessor.ProcessAsync(response, cancellationToken);
        }
    }
}