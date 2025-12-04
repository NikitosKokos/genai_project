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

                await _mongoContext.Sessions.InsertOneAsync(session);
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
    }
}
