using FinancialAdvisor.Application.Interfaces;
using FinancialAdvisor.Application.Models;
using FinancialAdvisor.Infrastructure.Data;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FinancialAdvisor.Infrastructure.Services.RAG
{
    public class RagOrchestrator : IRagService
    {
        private readonly IContextService _contextService;
        private readonly IMarketDataService _marketDataService;
        private readonly IEmbeddingService _embeddingService;
        private readonly IPromptService _promptService;
        private readonly ILLMService _llmService;
        private readonly IActionService _actionService;
        private readonly MongoDbContext _mongoContext;
        private readonly ILogger<RagOrchestrator> _logger;

        public RagOrchestrator(
            IContextService contextService,
            IMarketDataService marketDataService,
            IEmbeddingService embeddingService,
            IPromptService promptService,
            ILLMService llmService,
            IActionService actionService,
            MongoDbContext mongoContext,
            ILogger<RagOrchestrator> logger)
        {
            _contextService = contextService;
            _marketDataService = marketDataService;
            _embeddingService = embeddingService;
            _promptService = promptService;
            _llmService = llmService;
            _actionService = actionService;
            _mongoContext = mongoContext;
            _logger = logger;
        }

        public async Task<ChatResponse> ProcessQueryAsync(string userQuery, string sessionId)
        {
            _logger.LogInformation($"[{sessionId}] Processing RAG query: {userQuery}");

            // 1. Get Context
            var sessionTask = _contextService.GetSessionAsync(sessionId);
            var portfolioTask = _contextService.GetPortfolioAsync(sessionId);
            await Task.WhenAll(sessionTask, portfolioTask);
            
            var session = sessionTask.Result;
            var portfolio = portfolioTask.Result;
            
            var portfolioContext = _contextService.FormatPortfolioContext(portfolio);

            // 2. Get Market Data
            var symbols = portfolio?.Holdings?.Select(h => h.Symbol).ToList() ?? new List<string>();
            var marketData = await _marketDataService.GetMarketDataAsync(symbols);
            var marketContext = _marketDataService.FormatMarketContext(marketData);

            // 3. Retrieve Documents
            var queryEmbedding = await _embeddingService.EmbedAsync(userQuery);
            var relevantDocs = await VectorSearchAsync(queryEmbedding, 5);
            var ragContext = string.Join("\n\n", relevantDocs.Select(d => $"[{d.Source}] {d.Title}\n{d.Content}"));

            // 4. Construct Prompt
            var fullPrompt = _promptService.ConstructAugmentedUserPrompt(userQuery, portfolioContext, marketContext, ragContext, session);

            // 5. Generate Response
            var advice = await _llmService.GenerateFinancialAdviceAsync(userQuery, fullPrompt, sessionId);

            // 6. Execute Actions
            var trades = await _actionService.ParseAndExecuteTradesAsync(advice, sessionId);

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

        public async IAsyncEnumerable<string> ProcessQueryStreamAsync(string userQuery, string sessionId)
        {
             _logger.LogInformation($"[{sessionId}] Processing RAG STREAM query: {userQuery}");

            // 1. Get Context
            var session = await _contextService.GetSessionAsync(sessionId);
            var portfolio = await _contextService.GetPortfolioAsync(sessionId);
            var portfolioContext = _contextService.FormatPortfolioContext(portfolio);

            // 2. Get Market Data
            var symbols = portfolio?.Holdings?.Select(h => h.Symbol).ToList() ?? new List<string>();
            var marketData = await _marketDataService.GetMarketDataAsync(symbols);
            var marketContext = _marketDataService.FormatMarketContext(marketData);

            // 3. Retrieve Documents
            var queryEmbedding = await _embeddingService.EmbedAsync(userQuery);
            var relevantDocs = await VectorSearchAsync(queryEmbedding, 5);
            var ragContext = string.Join("\n\n", relevantDocs.Select(d => $"[{d.Source}] {d.Title}\n{d.Content}"));

            // 4. Construct Prompt
            var fullPrompt = _promptService.ConstructAugmentedUserPrompt(userQuery, portfolioContext, marketContext, ragContext, session);

            // 5. Stream Response
            await foreach (var chunk in _llmService.GenerateFinancialAdviceStreamAsync(userQuery, fullPrompt, sessionId))
            {
                yield return chunk;
            }
        }

        public async Task<List<FinancialDocument>> VectorSearchAsync(float[] queryEmbedding, int topK = 5)
        {
             try
            {
                var allDocs = await _mongoContext.FinancialDocuments
                    .Find(Builders<FinancialDocument>.Filter.Empty)
                    .ToListAsync();

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

        private double CosineSimilarity(float[] query, double[] docEmbedding)
        {
             if (query == null || docEmbedding == null) return 0;
            var length = Math.Min(query.Length, docEmbedding.Length);
            double dotProduct = 0, normA = 0, normB = 0;
            for (int i = 0; i < length; i++)
            {
                dotProduct += query[i] * docEmbedding[i];
                normA += query[i] * query[i];
                normB += docEmbedding[i] * docEmbedding[i];
            }
            if (normA == 0 || normB == 0) return 0;
            return dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
        }

        public async Task<Session> GetSessionContextAsync(string sessionId) => await _contextService.GetSessionAsync(sessionId);
        
        public async Task UpdatePortfolioFromTradeAsync(string sessionId, Trade trade)
        {
             await Task.CompletedTask; 
        }
    }
}
