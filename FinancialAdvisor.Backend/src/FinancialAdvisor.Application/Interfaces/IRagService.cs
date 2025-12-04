using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FinancialAdvisor.Application.Models;

namespace FinancialAdvisor.Application.Interfaces
{
    public interface IRagService
    {
        Task<object> QueryAsync(string query); // Legacy
        Task<ChatResponse> ProcessQueryAsync(string userQuery, string sessionId);
        IAsyncEnumerable<string> ProcessQueryStreamAsync(string userQuery, string sessionId, CancellationToken cancellationToken = default, bool enableReasoning = false, int documentCount = 3);
    }
}
