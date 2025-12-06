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
        
        // Trade Execution
        Task ExecuteBuyTradeAsync(string sessionId, string symbol, int quantity, decimal price);
        Task ExecuteSellTradeAsync(string sessionId, string symbol, int quantity, decimal price);
        Task RecordTradeAsync(string sessionId, Trade trade);
        
        // Pending Trade (for confirmation)
        Task SetPendingTradeAsync(string sessionId, PendingTrade pendingTrade);
        Task<PendingTrade> GetPendingTradeAsync(string sessionId);
        Task ClearPendingTradeAsync(string sessionId);
    }
}
