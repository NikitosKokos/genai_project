using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.ServiceModel.Syndication;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using FinancialAdvisor.Application.Interfaces;
using FinancialAdvisor.Application.Models;
using FinancialAdvisor.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace FinancialAdvisor.Infrastructure.Services
{
    public class NewsIngestionService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly MongoDbContext _mongoContext;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<NewsIngestionService> _logger;
        // Switch to Google News RSS for reliability
        private const string RSS_URL = "https://news.google.com/rss/search?q=stock+market+finance&hl=en-US&gl=US&ceid=US:en";
        private const int UPDATE_INTERVAL_HOURS = 4;
        private const int NEWS_RETENTION_HOURS = 24;

        public NewsIngestionService(
            IServiceProvider serviceProvider,
            MongoDbContext mongoContext,
            IHttpClientFactory httpClientFactory,
            ILogger<NewsIngestionService> logger)
        {
            _serviceProvider = serviceProvider;
            _mongoContext = mongoContext;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("NewsIngestionService: Service starting...");
            _logger.LogInformation("News Ingestion Service started.");

            // Yield to ensure app startup completes
            await Task.Yield();

            // Initial delay to allow dependent services (like Embedding) to become ready
            await Task.Delay(5000, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    Console.WriteLine($"NewsIngestionService: Fetching news from {RSS_URL}...");
                    await FetchAndIngestNewsAsync(stoppingToken);
                    await CleanupOldNewsAsync(stoppingToken);
                    Console.WriteLine("NewsIngestionService: Cycle complete.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"NewsIngestionService: Error - {ex.Message}");
                    _logger.LogError(ex, "Error in News Ingestion Service cycle.");
                }

                await Task.Delay(TimeSpan.FromHours(UPDATE_INTERVAL_HOURS), stoppingToken);
            }
        }

        private async Task FetchAndIngestNewsAsync(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var embeddingService = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();
            var client = _httpClientFactory.CreateClient();

            try 
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                
                // Use byte array to avoid encoding issues
                var data = await client.GetByteArrayAsync(RSS_URL, stoppingToken);
                using var stream = new System.IO.MemoryStream(data);
                using var reader = XmlReader.Create(stream);
                var feed = SyndicationFeed.Load(reader);

                int count = 0;
                foreach (var item in feed.Items.Take(20)) // Limit to 20 items
                {
                    if (stoppingToken.IsCancellationRequested) break;

                    var title = item.Title.Text;
                    var summary = item.Summary?.Text ?? item.Title.Text;
                    var link = item.Links.FirstOrDefault()?.Uri.ToString() ?? "";
                    var pubDate = item.PublishDate.DateTime;
                    if (pubDate == DateTime.MinValue) pubDate = DateTime.UtcNow;

                    // Check existence
                    var exists = await _mongoContext.FinancialDocuments.Find(
                        d => d.Title == title && d.Category == "News"
                    ).AnyAsync(stoppingToken);

                    if (!exists)
                    {
                        Console.WriteLine($"NewsIngestionService: Embedding '{title}'...");
                        var embedding = await embeddingService.EmbedAsync(title + " " + summary);
                        
                        var doc = new FinancialDocument
                        {
                            Title = title,
                            Content = summary,
                            Source = "Google News",
                            Category = "News",
                            CreatedAt = DateTime.UtcNow, 
                            Embedding = embedding.Select(f => (double)f).ToArray(),
                            Metadata = new BsonDocument 
                            {
                                { "link", link },
                                { "published_at", pubDate }
                            }
                        };

                        await _mongoContext.FinancialDocuments.InsertOneAsync(doc, cancellationToken: stoppingToken);
                        count++;
                    }
                }
                Console.WriteLine($"NewsIngestionService: Ingested {count} new items.");
            }
            catch (Exception ex)
            {
                 Console.WriteLine($"NewsIngestionService: Fetch failed: {ex.Message}");
                 _logger.LogError(ex, "Failed to fetch or parse RSS feed.");
            }
        }

        private async Task CleanupOldNewsAsync(CancellationToken stoppingToken)
        {
            var cutoff = DateTime.UtcNow.AddHours(-NEWS_RETENTION_HOURS);
            var filter = Builders<FinancialDocument>.Filter.And(
                Builders<FinancialDocument>.Filter.Eq(d => d.Category, "News"),
                Builders<FinancialDocument>.Filter.Lt(d => d.CreatedAt, cutoff)
            );

            var result = await _mongoContext.FinancialDocuments.DeleteManyAsync(filter, stoppingToken);
            if (result.DeletedCount > 0)
            {
                _logger.LogInformation($"Cleaned up {result.DeletedCount} old news items.");
            }
        }
    }
}
