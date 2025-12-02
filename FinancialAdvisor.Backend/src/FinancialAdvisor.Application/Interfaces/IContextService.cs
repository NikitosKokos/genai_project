using FinancialAdvisor.Application.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FinancialAdvisor.Application.Interfaces
{
    public interface IContextService
    {
        Task<Session> GetSessionAsync(string sessionId);
        Task<PortfolioSnapshot> GetPortfolioAsync(string sessionId);
        string FormatPortfolioContext(PortfolioSnapshot portfolio);
    }
}
