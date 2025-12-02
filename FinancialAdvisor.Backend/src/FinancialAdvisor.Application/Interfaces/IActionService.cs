using FinancialAdvisor.Application.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FinancialAdvisor.Application.Interfaces
{
    public interface IActionService
    {
        Task<List<Trade>> ParseAndExecuteTradesAsync(string llmResponse, string sessionId);
    }
}
