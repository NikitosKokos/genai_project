using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection; // Added this
using FinancialAdvisor.Infrastructure.Data;
using FinancialAdvisor.Application.Interfaces;
using FinancialAdvisor.Application.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FinancialAdvisor.Infrastructure.Services
{
    public class NewsIngestionService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<NewsIngestionService> _logger;
        private readonly TimeSpan _interval = TimeSpan.FromHours(1); // Run hourly

        public NewsIngestionService(IServiceProvider serviceProvider, ILogger<NewsIngestionService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("News Ingestion Service is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessNewsIngestionAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred executing News Ingestion.");
                }

                // Wait for next cycle
                await Task.Delay(_interval, stoppingToken);
            }
        }

        private async Task ProcessNewsIngestionAsync(CancellationToken stoppingToken)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var mongoContext = scope.ServiceProvider.GetRequiredService<MongoDbContext>();
                var embeddingService = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();

                _logger.LogInformation("Fetching latest market news...");

                // Mock News Feed - In production this would call an external News API (Bloomberg, Reuters, AlphaVantage)
                var newArticles = GetMockNewsArticles();

                foreach (var article in newArticles)
                {
                    if (stoppingToken.IsCancellationRequested) break;

                    // Check if article already exists (deduplication by Title for MVP)
                    var exists = await mongoContext.FinancialDocuments
                        .Find(d => d.Title == article.Title)
                        .AnyAsync(stoppingToken);

                    if (!exists)
                    {
                        _logger.LogInformation($"Ingesting article: {article.Title}");

                        // Generate Embedding
                        try
                        {
                            var embeddingFloat = await embeddingService.EmbedAsync(article.Content);
                            
                            // Convert float[] to double[] for storage if model requires double, 
                            // or keep consistent. Model has double[].
                            var embeddingDouble = Array.ConvertAll(embeddingFloat, x => (double)x);

                            var doc = new FinancialDocument
                            {
                                Id = ObjectId.GenerateNewId(),
                                Title = article.Title,
                                Content = article.Content,
                                Source = article.Source,
                                Category = "News",
                                CreatedAt = DateTime.UtcNow,
                                Embedding = embeddingDouble,
                                Metadata = new BsonDocument
                                {
                                    { "ingested_at", DateTime.UtcNow },
                                    { "sentiment", "neutral" } // Placeholder for sentiment analysis
                                }
                            };

                            await mongoContext.FinancialDocuments.InsertOneAsync(doc, cancellationToken: stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Failed to embed/save article: {article.Title}");
                        }
                    }
                }
            }
        }

        private List<MockArticle> GetMockNewsArticles()
        {
            var now = DateTime.UtcNow;
            return new List<MockArticle>
            {
                new MockArticle 
                { 
                    Title = "Fed Signals Potential Rate Cuts in late 2025", 
                    Content = "The Federal Reserve Chairman hinted at a possibility of interest rate cuts starting late 2025 if inflation metrics continue to show a downward trend towards the 2% target. Markets reacted positively with S&P 500 rising 1.2%.",
                    Source = "Financial Times"
                },
                new MockArticle 
                { 
                    Title = "Apple Unveils New AI Features for iPhone 17", 
                    Content = "Apple (AAPL) demonstrated new on-device generative AI capabilities for the upcoming iPhone 17 lineup, promising enhanced Siri interactions and real-time photo editing. Analysts predict a super-cycle upgrade.",
                    Source = "TechCrunch"
                },
                new MockArticle 
                { 
                    Title = "Oil Prices Surge Amidst Geopolitical Tensions", 
                    Content = "Crude oil futures jumped 4% today as supply chain concerns mounted following new geopolitical escalations in the Middle East. Energy stocks (XLE) saw significant inflows.",
                    Source = "Bloomberg"
                },
                 new MockArticle 
                { 
                    Title = "Tesla Misses Delivery Estimates, Stock Slides", 
                    Content = "Tesla (TSLA) reported Q4 deliveries of 450k vehicles, missing analyst expectations of 480k. The stock is down 5% in pre-market trading as concerns over EV demand softening grow.",
                    Source = "CNBC"
                }
            };
        }

        private class MockArticle
        {
            public string Title { get; set; }
            public string Content { get; set; }
            public string Source { get; set; }
        }
    }
}
