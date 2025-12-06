using FinancialAdvisor.Application.Interfaces;
using FinancialAdvisor.Application.Models;
using FinancialAdvisor.Infrastructure.Data;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FinancialAdvisor.Infrastructure.Services
{
    public class ContextService : IContextService
    {
        private readonly MongoDbContext _mongoContext;

        public ContextService(MongoDbContext mongoContext)
        {
            _mongoContext = mongoContext;
        }

        public async Task<Session> GetSessionAsync(string sessionId)
        {
            var session = await _mongoContext.Sessions
                .Find(s => s.SessionId == sessionId)
                .FirstOrDefaultAsync();

            if (session == null)
            {
                session = new Session
                {
                    SessionId = sessionId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    PortfolioContext = new PortfolioContext
                    {
                        RiskProfile = "moderate",
                        InvestmentGoal = "long_term_growth",
                        TotalPortfolioValue = 50000
                    },
                    Preferences = new BsonDocument()
                };

                // Use upsert to handle race conditions - if another request created the session, just use it
                try 
                {
                    var filter = Builders<Session>.Filter.Eq(s => s.SessionId, sessionId);
                    var options = new ReplaceOptions { IsUpsert = true };
                    await _mongoContext.Sessions.ReplaceOneAsync(filter, session, options);
                }
                catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
                {
                    // Another request created the session - fetch it
                    session = await _mongoContext.Sessions.Find(s => s.SessionId == sessionId).FirstOrDefaultAsync();
                }
            }

            return session;
        }

        public async Task<PortfolioSnapshot> GetPortfolioAsync(string sessionId)
        {
            return await _mongoContext.PortfolioSnapshots
                .Find(p => p.SessionId == sessionId)
                .SortByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public string FormatPortfolioContext(PortfolioSnapshot portfolio)
        {
            if (portfolio?.Holdings == null || !portfolio.Holdings.Any())
                return "Portfolio is empty.";

            var holdings = portfolio.Holdings
                .Select(h => $"- {h.Symbol}: {h.Quantity} shares @ ${h.CurrentPrice} avg cost: ${h.AvgCost}")
                .ToList();

            return $@"Current Portfolio:
{string.Join("\n", holdings)}
Total Value: ${portfolio.TotalValue}
Cash Balance: ${portfolio.CashBalance}";
        }

        public async Task AddChatMessageAsync(string sessionId, ChatMessage message)
        {
            message.SessionId = sessionId;
            if (message.Id == ObjectId.Empty) message.Id = ObjectId.GenerateNewId();
            if (message.CreatedAt == default) message.CreatedAt = DateTime.UtcNow;
            
            await _mongoContext.ChatHistory.InsertOneAsync(message);
        }

        public async Task<List<ChatMessage>> GetChatHistoryAsync(string sessionId, int limit = 6)
        {
            // Get last N messages, but we want them in chronological order for the prompt
            var messages = await _mongoContext.ChatHistory
                .Find(m => m.SessionId == sessionId)
                .SortByDescending(m => m.CreatedAt)
                .Limit(limit)
                .ToListAsync();

            messages.Reverse(); // Oldest first
            return messages;
        }

        public async Task<string> GetFinancialHealthSummaryAsync(string sessionId)
        {
            var session = await GetSessionAsync(sessionId);
            var portfolio = await GetPortfolioAsync(sessionId);
            return BuildFinancialHealthSummary(session, portfolio);
        }

        public string BuildFinancialHealthSummary(Session session, PortfolioSnapshot portfolio)
        {
            if (portfolio == null)
            {
                return $"User Profile: {session?.PortfolioContext?.RiskProfile ?? "Unknown"} risk profile. Goal: {session?.PortfolioContext?.InvestmentGoal ?? "Not set"}. Portfolio is currently empty or not linked.";
            }

            // Calculate basic metrics
            var totalValue = portfolio.TotalValue;
            var cash = portfolio.CashBalance;
            var cashRatio = totalValue > 0 ? (cash / totalValue) * 100 : 0;
            
            var holdingsCount = portfolio.Holdings?.Count ?? 0;
            var topHolding = portfolio.Holdings?.OrderByDescending(h => h.Quantity * h.CurrentPrice).FirstOrDefault();
            
            return $@"Financial Health Summary:
- Total Assets: ${totalValue:N0}
- Cash Position: ${cash:N0} ({cashRatio:F1}% of portfolio)
- Investment Strategy: {session?.PortfolioContext?.InvestmentGoal?.Replace("_", " ") ?? "General Investing"}
- Risk Tolerance: {session?.PortfolioContext?.RiskProfile ?? "Moderate"}
- Active Positions: {holdingsCount} holdings
{(topHolding != null ? $"- Top Allocation: {topHolding.Symbol} (${(topHolding.Quantity * topHolding.CurrentPrice):N0})" : "")}
This context should be used to ground all financial advice.";
        }

        public async Task UpdateMetadataAsync(string sessionId, string symbol = null, string tool = null)
        {
            var session = await GetSessionAsync(sessionId);
            if (session.Metadata == null)
                session.Metadata = new ConversationMetadata();
            
            if (symbol != null) session.Metadata.LastSymbol = symbol;
            if (tool != null) session.Metadata.LastTool = tool;
            session.Metadata.LastActionTimestamp = DateTime.UtcNow;
            session.Metadata.ToolCallCount++;
            
            // Note: In a real app, use partial update (Builders<Session>.Update) to avoid overwriting other fields.
            // For MVP, ReplaceOne is acceptable if we fetch fresh first.
            await _mongoContext.Sessions.ReplaceOneAsync(
                s => s.SessionId == sessionId, 
                session);
        }
    }
}
