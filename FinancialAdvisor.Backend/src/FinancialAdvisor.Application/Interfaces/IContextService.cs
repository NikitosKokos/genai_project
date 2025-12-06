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
        
        // Chat History
        Task AddChatMessageAsync(string sessionId, ChatMessage message);
        Task<List<ChatMessage>> GetChatHistoryAsync(string sessionId, int limit = 6);
        
        // Semantic Context
        Task<string> GetFinancialHealthSummaryAsync(string sessionId);
        string BuildFinancialHealthSummary(Session session, PortfolioSnapshot portfolio);

        // Metadata
        Task UpdateMetadataAsync(string sessionId, string symbol = null, string tool = null);
    }
}
