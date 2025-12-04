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
             var sw = Stopwatch.StartNew();
             _logger.LogInformation($"[{sessionId}] Start processing query: {userQuery} (Reasoning: {enableReasoning}, Docs: {documentCount})");

            // 1. Start independent tasks parallelly
            // RAG (Embed + Search + News)
            var ragTask = Task.Run(async () => 
            {
                var swRag = Stopwatch.StartNew();
                var queryEmbedding = await _embeddingService.EmbedAsync(userQuery);
                
                // Parallelize Vector Search and News Fetch
                var vectorSearchTask = VectorSearchAsync(queryEmbedding, documentCount);
                var newsTask = GetRecentNewsAsync(5); // Fetch top 5 news
                
                await Task.WhenAll(vectorSearchTask, newsTask);
                
                var docs = await vectorSearchTask;
                var news = await newsTask;

                swRag.Stop();
                _logger.LogInformation($"[{sessionId}] RAG retrieval took {swRag.ElapsedMilliseconds}ms");
                
                // Format Documents
                var docContent = string.Join("\n\n", docs.Select(d => $"[{d.Source}] {d.Title}\n{d.Content}"));
                
                // Format News (Clean up titles)
                var newsContent = string.Join("\n", news.Select(d => 
                    $"- {CleanTitle(d.Title)}"));
                
                if (string.IsNullOrEmpty(newsContent)) return docContent;
                if (string.IsNullOrEmpty(docContent)) return newsContent;
                
                return "=== LATEST HEADLINES ===\n" + newsContent + "\n\n=== RELEVANT DOCUMENTS ===\n" + docContent;
            }, cancellationToken);

            // Session & Portfolio
            var sessionTask = _contextService.GetSessionAsync(sessionId);
            var portfolioTask = _contextService.GetPortfolioAsync(sessionId);

            await Task.WhenAll(sessionTask, portfolioTask);
            _logger.LogInformation($"[{sessionId}] Context loaded in {sw.ElapsedMilliseconds}ms");

            var session = await sessionTask;
            var portfolio = await portfolioTask;
            var portfolioContext = _contextService.FormatPortfolioContext(portfolio);

            // Market Data (Portfolio + Query Symbols)
            var portfolioSymbols = portfolio?.Holdings?.Select(h => h.Symbol).ToList() ?? new List<string>();
            var querySymbols = ExtractSymbolsFromQuery(userQuery);
            var allSymbols = portfolioSymbols.Concat(querySymbols).Distinct().ToList();

            var marketDataTask = _marketDataService.GetMarketDataAsync(allSymbols);

            // Wait for RAG and Market Data
            await Task.WhenAll(ragTask, marketDataTask);
            
            var ragContext = await ragTask;
            var marketData = await marketDataTask;
            var marketContext = _marketDataService.FormatMarketContext(marketData);

            _logger.LogInformation($"[{sessionId}] Data gathering complete in {sw.ElapsedMilliseconds}ms. Constructing prompt...");

            // 4. Construct Prompt
            var fullPrompt = _promptService.ConstructAugmentedUserPrompt(userQuery, portfolioContext, marketContext, ragContext, session);

            _logger.LogInformation($"[{sessionId}] Starting LLM stream...");
            var llmSw = Stopwatch.StartNew();

            // 5. Stream Response
            bool jsonStarted = false;
            await foreach (var chunk in _llmService.GenerateFinancialAdviceStreamAsync(userQuery, fullPrompt, sessionId, cancellationToken, enableReasoning))
            {
                if (llmSw.IsRunning)  
                {
                    llmSw.Stop();
                    _logger.LogInformation($"[{sessionId}] Time to First Token (TTFT): {llmSw.ElapsedMilliseconds}ms");
                }
                
                if (chunk.Contains("{")) jsonStarted = true;
                yield return chunk;
            }
            
            // FORCE JSON FALLBACK if model failed to output it
            if (!jsonStarted)
            {
                yield return "\n\n" + @"{ ""trades"": [], ""disclaimer_required"": true, ""intent"": ""INFO"" }";
            }

            _logger.LogInformation($"[{sessionId}] Total processing time: {sw.ElapsedMilliseconds}ms");
        }

        private List<string> ExtractSymbolsFromQuery(string query)
        {
            var candidates = new HashSet<string>();
            
            // 1. Known Entities
            var knownEntities = new[] { 
                "Apple", "Microsoft", "Google", "Amazon", "Tesla", "Nvidia", "Meta", "Facebook", "Netflix", "Bitcoin", "Ethereum", 
                "AAPL", "MSFT", "GOOGL", "AMZN", "TSLA", "NVDA", "META", "NFLX", "BTC", "ETH" 
            };
            
            foreach (var entity in knownEntities)
            {
                if (query.IndexOf(entity, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    candidates.Add(entity);
                }
            }

            // 2. Common Typos (Manual fix for user requests)
            var commonTypos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) 
            {
                { "Mircosoft", "Microsoft" },
                { "Aple", "Apple" },
                { "Aplle", "Apple" },
                { "Gogle", "Google" },
                { "Nividia", "Nvidia" }
            };

            foreach (var typo in commonTypos)
            {
                 if (query.IndexOf(typo.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    candidates.Add(typo.Value);
                }
            }

            // 3. Potential Tickers (2-5 uppercase letters surrounded by non-letters or start/end)
            // This is a heuristic to catch things like "AMD", "INTC" if typed in uppercase.
            var regex = new Regex(@"\b[A-Z]{2,5}\b");
            foreach (Match match in regex.Matches(query))
            {
                candidates.Add(match.Value);
            }

            return candidates.ToList();
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

        private async Task<List<FinancialDocument>> GetRecentNewsAsync(int count)
        {
            try
            {
                return await _mongoContext.FinancialDocuments
                    .Find(d => d.Category == "News")
                    .SortByDescending(d => d.CreatedAt)
                    .Limit(count)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch recent news");
                return new List<FinancialDocument>();
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

        private string CleanTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return "";
            // Remove common suffixes
            var clean = title
                .Replace(" - Yahoo Finance", "")
                .Replace(" - Yahoo! Finance", "")
                .Replace(" - Google News", "")
                .Replace(" - CNBC", "")
                .Replace(" - Bloomberg", "");
            
            // Remove anything that looks like a base64 hash [CBM...]
            if (clean.StartsWith("[CBM"))
            {
                var idx = clean.IndexOf("] - ");
                if (idx > 0) clean = clean.Substring(idx + 4);
            }
            
            return clean.Trim();
        }
    }
}