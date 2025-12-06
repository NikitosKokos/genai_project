using FinancialAdvisor.Application.Interfaces;
using FinancialAdvisor.Application.Models;
using FinancialAdvisor.Infrastructure.Data;
using MongoDB.Bson;
using MongoDB.Driver;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FinancialAdvisor.Infrastructure.Services
{
    public class ContextService : IContextService
    {
        private readonly MongoDbContext _mongoContext;
        private readonly ILogger<ContextService> _logger;

        public ContextService(MongoDbContext mongoContext, ILogger<ContextService> logger)
        {
            _mongoContext = mongoContext;
            _logger = logger;
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
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                throw new ArgumentException("SessionId cannot be null or empty", nameof(sessionId));
            }

            // Use explicit filter builder to ensure correct field mapping (session_id in MongoDB)
            var filter = Builders<PortfolioSnapshot>.Filter.Eq("session_id", sessionId);
            
            _logger.LogInformation($"[ContextService] Querying portfolio for sessionId: '{sessionId}'");
            
            var portfolio = await _mongoContext.PortfolioSnapshots
                .Find(filter)
                .SortByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync();

            // Log for debugging
            if (portfolio == null)
            {
                _logger.LogWarning($"[ContextService] No portfolio found for sessionId: '{sessionId}'. Check if MongoDB has portfolio data for this session.");
                
                // Try to verify the database connection and collection
                var count = await _mongoContext.PortfolioSnapshots.CountDocumentsAsync(FilterDefinition<PortfolioSnapshot>.Empty);
                _logger.LogInformation($"[ContextService] Total portfolios in collection: {count}");
            }
            else
            {
                var holdingsCount = portfolio.Holdings?.Count ?? 0;
                _logger.LogInformation($"[ContextService] Portfolio found for sessionId: '{sessionId}' - Holdings: {holdingsCount}, TotalValue: {portfolio.TotalValue}, CashBalance: {portfolio.CashBalance}");
                
                if (holdingsCount > 0 && portfolio.Holdings != null)
                {
                    var symbols = string.Join(", ", portfolio.Holdings.Select(h => h.Symbol));
                    _logger.LogInformation($"[ContextService] Portfolio holdings: {symbols}");
                }
            }

            return portfolio;
        }

        public string FormatPortfolioContext(PortfolioSnapshot portfolio)
        {
            if (portfolio == null)
            {
                _logger.LogWarning("[ContextService] FormatPortfolioContext: portfolio is null");
                return "Portfolio is empty or not found.";
            }

            if (portfolio.Holdings == null || !portfolio.Holdings.Any())
            {
                _logger.LogWarning($"[ContextService] FormatPortfolioContext: portfolio exists but holdings is null or empty. TotalValue: {portfolio.TotalValue}, CashBalance: {portfolio.CashBalance}");
                return $"Portfolio is empty. Cash Balance: ${portfolio.CashBalance:N2}";
            }

            var holdings = portfolio.Holdings
                .Select(h => $"- {h.Symbol}: {h.Quantity} shares @ ${h.CurrentPrice:N2} (avg cost: ${h.AvgCost:N2})")
                .ToList();

            var result = $@"Current Portfolio:
{string.Join("\n", holdings)}
Total Value: ${portfolio.TotalValue:N2}
Cash Balance: ${portfolio.CashBalance:N2}";

            _logger.LogInformation($"[ContextService] FormatPortfolioContext: Formatted portfolio with {holdings.Count} holdings");
            return result;
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
                _logger.LogWarning($"[ContextService] BuildFinancialHealthSummary: portfolio is null for sessionId: {session?.SessionId ?? "unknown"}");
                return $"User Profile: {session?.PortfolioContext?.RiskProfile ?? "Unknown"} risk profile. Goal: {session?.PortfolioContext?.InvestmentGoal ?? "Not set"}. Portfolio is currently empty or not linked.";
            }

            // Calculate basic metrics
            var totalValue = portfolio.TotalValue;
            var cash = portfolio.CashBalance;
            var cashRatio = totalValue > 0 ? (cash / totalValue) * 100 : 0;
            
            var holdingsCount = portfolio.Holdings?.Count ?? 0;
            var topHolding = portfolio.Holdings?.OrderByDescending(h => h.Quantity * h.CurrentPrice).FirstOrDefault();
            
            if (holdingsCount == 0)
            {
                _logger.LogWarning($"[ContextService] BuildFinancialHealthSummary: portfolio exists but has 0 holdings. TotalValue: {totalValue}, CashBalance: {cash}");
            }
            
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

        public async Task ExecuteBuyTradeAsync(string sessionId, string symbol, int quantity, decimal price)
        {
            var portfolio = await GetPortfolioAsync(sessionId);
            if (portfolio == null)
            {
                // Create new portfolio
                portfolio = new PortfolioSnapshot
                {
                    SessionId = sessionId,
                    Holdings = new List<Holding>(),
                    CashBalance = 100000, // Default starting cash
                    TotalValue = 100000,
                    CreatedAt = DateTime.UtcNow
                };
            }

            var totalCost = price * quantity;
            
            // Validate sufficient funds BEFORE executing trade
            if (portfolio.CashBalance < totalCost)
            {
                throw new InvalidOperationException(
                    $"Insufficient funds. Required: ${totalCost:N2}, Available: ${portfolio.CashBalance:N2}");
            }
            
            // Update cash balance
            portfolio.CashBalance -= totalCost;
            
            // Update or add holding
            var holding = portfolio.Holdings?.FirstOrDefault(h => 
                string.Equals(h.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
            
            if (holding == null)
            {
                if (portfolio.Holdings == null)
                    portfolio.Holdings = new List<Holding>();
                
                portfolio.Holdings.Add(new Holding
                {
                    Symbol = symbol,
                    Quantity = quantity,
                    AvgCost = price,
                    CurrentPrice = price
                });
            }
            else
            {
                // Calculate new average cost
                var totalCostOld = holding.AvgCost * holding.Quantity;
                var totalCostNew = totalCostOld + totalCost;
                var totalQuantity = holding.Quantity + quantity;
                holding.AvgCost = totalCostNew / totalQuantity;
                holding.Quantity = totalQuantity;
                holding.CurrentPrice = price;
            }

            // Update total value
            portfolio.TotalValue = portfolio.CashBalance + 
                (portfolio.Holdings?.Sum(h => h.Quantity * h.CurrentPrice) ?? 0);
            
            portfolio.CreatedAt = DateTime.UtcNow;

            // Upsert portfolio
            var filter = Builders<PortfolioSnapshot>.Filter.Eq("session_id", sessionId);
            var options = new ReplaceOptions { IsUpsert = true };
            await _mongoContext.PortfolioSnapshots.ReplaceOneAsync(filter, portfolio, options);
            
            _logger.LogInformation($"[{sessionId}] BUY executed: {quantity} {symbol} @ ${price:N2}, New cash: ${portfolio.CashBalance:N2}");
        }

        public async Task ExecuteSellTradeAsync(string sessionId, string symbol, int quantity, decimal price)
        {
            var portfolio = await GetPortfolioAsync(sessionId);
            if (portfolio == null || portfolio.Holdings == null)
            {
                throw new InvalidOperationException("Portfolio not found or has no holdings");
            }

            var totalProceeds = price * quantity;
            
            // Update cash balance
            portfolio.CashBalance += totalProceeds;
            
            // Update holding
            var holding = portfolio.Holdings.FirstOrDefault(h => 
                string.Equals(h.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
            
            if (holding == null || holding.Quantity < quantity)
            {
                throw new InvalidOperationException($"Insufficient shares of {symbol}");
            }

            holding.Quantity -= quantity;
            holding.CurrentPrice = price;

            // Remove holding if quantity is zero
            if (holding.Quantity == 0)
            {
                portfolio.Holdings.Remove(holding);
            }

            // Update total value
            portfolio.TotalValue = portfolio.CashBalance + 
                (portfolio.Holdings?.Sum(h => h.Quantity * h.CurrentPrice) ?? 0);
            
            portfolio.CreatedAt = DateTime.UtcNow;

            // Update portfolio
            var filter = Builders<PortfolioSnapshot>.Filter.Eq("session_id", sessionId);
            await _mongoContext.PortfolioSnapshots.ReplaceOneAsync(filter, portfolio);
            
            _logger.LogInformation($"[{sessionId}] SELL executed: {quantity} {symbol} @ ${price:N2}, New cash: ${portfolio.CashBalance:N2}");
        }

        public async Task RecordTradeAsync(string sessionId, Trade trade)
        {
            var tradingHistory = await _mongoContext.TradingHistory
                .Find(t => t.SessionId == sessionId)
                .FirstOrDefaultAsync();

            if (tradingHistory == null)
            {
                tradingHistory = new TradingHistory
                {
                    SessionId = sessionId,
                    Trades = new List<Trade> { trade }
                };
                await _mongoContext.TradingHistory.InsertOneAsync(tradingHistory);
            }
            else
            {
                if (tradingHistory.Trades == null)
                    tradingHistory.Trades = new List<Trade>();
                
                tradingHistory.Trades.Add(trade);
                var update = Builders<TradingHistory>.Update.Set(t => t.Trades, tradingHistory.Trades);
                await _mongoContext.TradingHistory.UpdateOneAsync(
                    t => t.SessionId == sessionId,
                    update);
            }
        }

        public async Task SetPendingTradeAsync(string sessionId, PendingTrade pendingTrade)
        {
            var session = await GetSessionAsync(sessionId);
            if (session.Metadata == null)
                session.Metadata = new ConversationMetadata();
            
            session.Metadata.PendingTrade = pendingTrade;
            
            await _mongoContext.Sessions.ReplaceOneAsync(
                s => s.SessionId == sessionId,
                session);
        }

        public async Task<PendingTrade> GetPendingTradeAsync(string sessionId)
        {
            var session = await GetSessionAsync(sessionId);
            return session.Metadata?.PendingTrade;
        }

        public async Task ClearPendingTradeAsync(string sessionId)
        {
            var session = await GetSessionAsync(sessionId);
            if (session.Metadata != null)
            {
                session.Metadata.PendingTrade = null;
                await _mongoContext.Sessions.ReplaceOneAsync(
                    s => s.SessionId == sessionId,
                    session);
            }
        }
    }
}
