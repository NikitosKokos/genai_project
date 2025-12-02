using MongoDB.Bson;
using MongoDB.Driver;
using FinancialAdvisor.Application.Models;
using FinancialAdvisor.Infrastructure.Data;
using FinancialAdvisor.Application.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;

namespace FinancialAdvisor.Infrastructure.Services.RAG
{
    public class MongoDBRAGService : IRagService
    {
        private readonly MongoDbContext _mongoContext;
        private readonly IEmbeddingService _embeddingService;
        private readonly ILLMService _llmService;
        private readonly IMarketDataService _marketDataService; // Kept but might not be used if we rely on cache
        private readonly ILogger<MongoDBRAGService> _logger;

        public MongoDBRAGService(
            MongoDbContext mongoContext,
            IEmbeddingService embeddingService,
            ILLMService llmService,
            IMarketDataService marketDataService,
            ILogger<MongoDBRAGService> logger)
        {
            _mongoContext = mongoContext;
            _embeddingService = embeddingService;
            _llmService = llmService;
            _marketDataService = marketDataService;
            _logger = logger;
        }

        public async Task<ChatResponse> ProcessQueryAsync(string userQuery, string sessionId)
        {
            try
            {
                _logger.LogInformation($"[{sessionId}] Processing RAG query: {userQuery}");

                // 1. Generate embedding for user query
                var queryEmbedding = await _embeddingService.EmbedAsync(userQuery);
                _logger.LogInformation($"[{sessionId}] Generated query embedding");

                // 2. Vector similarity search in MongoDB
                var relevantDocs = await VectorSearchAsync(queryEmbedding, topK: 5);
                _logger.LogInformation($"[{sessionId}] Retrieved {relevantDocs.Count} relevant documents");

                // 3. Get session and portfolio context
                var session = await GetSessionContextAsync(sessionId);
                if (session == null)
                {
                    session = await CreateDefaultSessionAsync(sessionId);
                }

                // 4. Get current portfolio holdings
                var portfolioSnapshot = await GetLatestPortfolioAsync(sessionId);
                var portfolioContext = FormatPortfolioContext(portfolioSnapshot);

                // 5. Get current market prices
                var marketData = await GetMarketDataAsync(
                    portfolioSnapshot?.Holdings?.Select(h => h.Symbol).ToList() ?? new List<string>()
                );
                var marketContext = FormatMarketContext(marketData);

                // 6. Aggregate all context
                var aggregatedContext = BuildRagContext(
                    relevantDocs,
                    portfolioContext,
                    marketContext,
                    session
                );

                _logger.LogInformation($"[{sessionId}] Aggregated RAG context ready");

                // 7. Generate advice via LLM
                var advice = await _llmService.GenerateFinancialAdviceAsync(
                    userQuery,
                    aggregatedContext,
                    sessionId
                );

                _logger.LogInformation($"[{sessionId}] Generated LLM response");

                // 8. Parse and execute mock trades
                var trades = ParseTradesFromResponse(advice);
                foreach (var trade in trades)
                {
                    await ExecuteMockTradeAsync(sessionId, trade);
                }

                // 9. Log RAG query for analytics
                await LogRagQueryAsync(sessionId, userQuery, relevantDocs.Count);

                return new ChatResponse
                {
                    Advice = advice,
                    ExecutedTrades = trades,
                    Sources = relevantDocs.Select(d => new DocumentSource
                    {
                        Title = d.Title,
                        Source = d.Source,
                        Category = d.Category
                    }).ToList(),
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[{sessionId}] RAG processing failed");
                throw;
            }
        }

        // Vector similarity search in MongoDB
        public async Task<List<FinancialDocument>> VectorSearchAsync(float[] queryEmbedding, int topK = 5)
        {
            try
            {
                // MongoDB Community Edition doesn't have native vector search (requires Atlas).
                // Simulating via brute force for MVP as per guide logic
                
                var allDocs = await _mongoContext.FinancialDocuments
                    .Find(Builders<FinancialDocument>.Filter.Empty)
                    .ToListAsync();

                // Calculate cosine similarity for each document
                var scoredDocs = allDocs
                    .Select(doc => new
                    {
                        Document = doc,
                        Similarity = CosineSimilarity(queryEmbedding, doc.Embedding)
                    })
                    .OrderByDescending(x => x.Similarity)
                    .Take(topK)
                    .Select(x => x.Document)
                    .ToList();

                return scoredDocs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Vector search failed");
                throw;
            }
        }

        // Get session context
        public async Task<Session> GetSessionContextAsync(string sessionId)
        {
            var session = await _mongoContext.Sessions
                .Find(s => s.SessionId == sessionId)
                .FirstOrDefaultAsync();

            return session;
        }

        public async Task UpdatePortfolioFromTradeAsync(string sessionId, Trade trade)
        {
             // This method was in interface but not fully detailed in guide for implementation.
             // Assuming it updates snapshot based on trade.
             // For now, leaving empty or implementing basics if needed.
             await Task.CompletedTask;
        }

        // Get latest portfolio snapshot
        private async Task<PortfolioSnapshot> GetLatestPortfolioAsync(string sessionId)
        {
            var portfolio = await _mongoContext.PortfolioSnapshots
                .Find(p => p.SessionId == sessionId)
                .SortByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync();

            return portfolio;
        }

        // Get market data for symbols
        private async Task<List<MarketDataCache>> GetMarketDataAsync(List<string> symbols)
        {
            if (!symbols.Any())
                return new List<MarketDataCache>();

            var marketData = await _mongoContext.MarketCache
                .Find(m => symbols.Contains(m.Symbol))
                .ToListAsync();

            return marketData;
        }

        // Format portfolio for RAG context
        private string FormatPortfolioContext(PortfolioSnapshot portfolio)
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

        // Format market data for RAG context
        private string FormatMarketContext(List<MarketDataCache> marketData)
        {
            if (!marketData.Any())
                return "No market data available.";

            var prices = marketData
                .Select(m => $"- {m.Symbol}: ${m.Price} ({m.ChangePercent:+0.00;-0.00}%)")
                .ToList();

            return $@"Current Market Prices:
{string.Join("\n", prices)}";
        }

        // Build complete RAG context
        private string BuildRagContext(
            List<FinancialDocument> relevantDocs,
            string portfolioContext,
            string marketContext,
            Session session)
        {
            var newsContext = string.Join("\n\n", 
                relevantDocs.Select(d => $"[{d.Source}] {d.Title}\n{d.Content}")
            );

            var sessionContext = session?.PortfolioContext?.RiskProfile ?? "moderate";

            return $@"
=== FINANCIAL ADVISOR RAG CONTEXT ===

Current Portfolio:
{portfolioContext}

Market Prices:
{marketContext}

Risk Profile: {sessionContext}

Recent Financial News & Analysis:
{newsContext}

=== END CONTEXT ===

Based on the above context, provide personalized financial advice.";
        }

        // Execute mock trade and update portfolio
        private async Task ExecuteMockTradeAsync(string sessionId, Trade trade)
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
                var update = Builders<TradingHistory>.Update.Push(t => t.Trades, trade);
                await _mongoContext.TradingHistory.UpdateOneAsync(
                    t => t.SessionId == sessionId,
                    update
                );
            }

            _logger.LogInformation($"[{sessionId}] Executed mock trade: {trade.Action} {trade.Quantity} {trade.Symbol} @ ${trade.Price}");
        }

        // Parse trades from LLM response
        private List<Trade> ParseTradesFromResponse(string response)
        {
            var trades = new List<Trade>();
            try
            {
                // Basic attempt to find JSON block in response
                var startIndex = response.IndexOf("{");
                var endIndex = response.LastIndexOf("}");
                if (startIndex >= 0 && endIndex > startIndex)
                {
                    var json = response.Substring(startIndex, endIndex - startIndex + 1);
                    // Expecting {"trades": [...]} or just array or single object
                    using (var doc = JsonDocument.Parse(json))
                    {
                        if (doc.RootElement.TryGetProperty("trades", out var tradesElement) && tradesElement.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var element in tradesElement.EnumerateArray())
                            {
                                var trade = new Trade
                                {
                                    Symbol = element.GetProperty("symbol").GetString(),
                                    Action = element.GetProperty("action").GetString(),
                                    Quantity = element.GetProperty("qty").GetInt32(), // Guide used "qty" in example json
                                    Price = 0, // Need lookup
                                    ExecutedAt = DateTime.UtcNow
                                };
                                trades.Add(trade);
                            }
                        }
                    }
                }
            }
            catch
            {
                // _logger.LogWarning("Could not parse trades from LLM response");
            }
            return trades;
        }

        // Log RAG query for analytics
        private async Task LogRagQueryAsync(string sessionId, string query, int retrievedDocs)
        {
            var ragLog = new RagQuery
            {
                SessionId = sessionId,
                Query = query,
                RetrievedDocs = retrievedDocs,
                ResponseQualityScore = 0.85m,  // Placeholder
                ExecutionTimeMs = 250,  // Placeholder
                CreatedAt = DateTime.UtcNow
            };

            await _mongoContext.RagQueries.InsertOneAsync(ragLog);
        }

        // Create default session if not exists
        private async Task<Session> CreateDefaultSessionAsync(string sessionId)
        {
            var session = new Session
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
            return session;
        }

        // Cosine similarity calculation
        private double CosineSimilarity(float[] query, double[] docEmbedding)
        {
            if (query == null || docEmbedding == null) return 0;
            // Allow for some flexibility if dimensions mismatch slightly in mock, but they should match
            var length = Math.Min(query.Length, docEmbedding.Length);

            double dotProduct = 0;
            double normA = 0;
            double normB = 0;

            for (int i = 0; i < length; i++)
            {
                dotProduct += query[i] * docEmbedding[i];
                normA += query[i] * query[i];
                normB += docEmbedding[i] * docEmbedding[i];
            }

            if (normA == 0 || normB == 0) return 0;

            return dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
        }
    }
}
