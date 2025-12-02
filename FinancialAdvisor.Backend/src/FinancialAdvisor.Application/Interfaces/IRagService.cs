using FinancialAdvisor.Application.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FinancialAdvisor.Application.Interfaces
{
    public interface IRagService
    {
        Task<ChatResponse> ProcessQueryAsync(string userQuery, string sessionId);
        Task<List<FinancialDocument>> VectorSearchAsync(float[] queryEmbedding, int topK = 5);
        Task<Session> GetSessionContextAsync(string sessionId);
        Task UpdatePortfolioFromTradeAsync(string sessionId, Trade trade);
    }

    public class ChatResponse
    {
        public string Advice { get; set; }
        public List<Trade> ExecutedTrades { get; set; }
        public List<DocumentSource> Sources { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class DocumentSource
    {
        public string Title { get; set; }
        public string Source { get; set; }
        public string Category { get; set; }
    }
}
