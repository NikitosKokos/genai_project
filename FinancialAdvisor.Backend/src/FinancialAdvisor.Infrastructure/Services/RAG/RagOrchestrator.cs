using FinancialAdvisor.Application.Interfaces;
using FinancialAdvisor.Application.Models;
using FinancialAdvisor.Infrastructure.Data;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Text.RegularExpressions;

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
        
        public async Task<object> QueryAsync(string query)
        {
             return await ProcessQueryAsync(query, "default-session");
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

            // 2. Get Market Data (Portfolio + Query Symbols)
            var portfolioSymbols = portfolio?.Holdings?.Select(h => h.Symbol).ToList() ?? new List<string>();
            var querySymbols = ExtractSymbolsFromQuery(userQuery);
            var allSymbols = portfolioSymbols.Concat(querySymbols).Distinct().ToList();

            var marketData = await _marketDataService.GetMarketDataAsync(allSymbols);
            var marketContext = _marketDataService.FormatMarketContext(marketData);

            // 3. Retrieve Documents
            var queryEmbedding = await _embeddingService.EmbedAsync(userQuery);
            var relevantDocs = await VectorSearchAsync(queryEmbedding, 3);
            var ragContext = string.Join("\n\n", relevantDocs.Select(d => $"[{d.Source}] {d.Title}\n{d.Content}"));

            // 4. Construct Prompt
            var fullPrompt = _promptService.ConstructAugmentedUserPrompt(userQuery, portfolioContext, marketContext, ragContext, session);

            // 5. Generate Response
            var adviceRaw = await _llmService.GenerateFinancialAdviceAsync(userQuery, fullPrompt, sessionId);
            var advice = _promptService.PostProcessModelOutput(adviceRaw);

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

        public async IAsyncEnumerable<string> ProcessQueryStreamAsync(string userQuery, string sessionId, [EnumeratorCancellation] CancellationToken cancellationToken = default, bool enableReasoning = false, int documentCount = 3)
        {
             var response = await ProcessQueryAsync(userQuery, sessionId);
             yield return response.Advice;
        }

        private List<string> ExtractSymbolsFromQuery(string query)
        {
            var candidates = new HashSet<string>();
            if (query.Contains("AAPL")) candidates.Add("AAPL");
            return candidates.ToList();
        }

        public async Task<List<FinancialDocument>> VectorSearchAsync(float[] queryEmbedding, int topK = 5)
        {
            try
            {
                var allDocs = await _mongoContext.FinancialDocuments
                    .Find(Builders<FinancialDocument>.Filter.Empty)
                    .ToListAsync();

                return allDocs
                    .OrderByDescending(d => CosineSimilarity(queryEmbedding, d.Embedding))
                    .Take(topK)
                    .ToList();
            }
            catch
            {
                return new List<FinancialDocument>();
            }
        }

        private async Task<List<FinancialDocument>> GetRecentNewsAsync(int count)
        {
             return await _mongoContext.FinancialDocuments
                .Find(d => d.Category == "News")
                .SortByDescending(d => d.CreatedAt)
                .Limit(count)
                .ToListAsync();
        }

        private double CosineSimilarity(float[] query, double[] docEmbedding)
        {
            if (query == null || docEmbedding == null) return 0;
            // Simple dot product for mock
            return query.Zip(docEmbedding, (a, b) => a * b).Sum();
        }

        public async Task<Session> GetSessionContextAsync(string sessionId) => await _contextService.GetSessionAsync(sessionId);
        
        public async Task UpdatePortfolioFromTradeAsync(string sessionId, Trade trade)
        {
             await Task.CompletedTask; 
        }

        private string CleanTitle(string title) 
        { 
            return title; 
        }
    }
}
