using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FinancialAdvisor.Application.Interfaces
{
    public interface ILLMService
    {
        Task<string> GenerateFinancialAdviceAsync(string userQuery, string context, string sessionId);
        IAsyncEnumerable<string> GenerateFinancialAdviceStreamAsync(string userQuery, string context, string sessionId, CancellationToken cancellationToken = default);
    }
}

