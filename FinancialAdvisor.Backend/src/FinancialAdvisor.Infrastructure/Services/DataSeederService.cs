using FinancialAdvisor.Application.Models;
using FinancialAdvisor.Infrastructure.Data;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FinancialAdvisor.Infrastructure.Services
{
    public class DataSeederService : IHostedService
    {
        private readonly MongoDbContext _mongoContext;
        private readonly ILogger<DataSeederService> _logger;

        public DataSeederService(MongoDbContext mongoContext, ILogger<DataSeederService> logger)
        {
            _mongoContext = mongoContext;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Seeding initial data...");

            try
            {
                // Seed Assets if empty
                var assetsCount = await _mongoContext.Assets.CountDocumentsAsync(FilterDefinition<AssetDefinition>.Empty, cancellationToken: cancellationToken);
                if (assetsCount == 0)
                {
                    var initialAssets = new List<AssetDefinition>
                    {
                        new AssetDefinition { Symbol = "AAPL", Name = "Apple Inc.", Sector = "Technology", Type = "Stock", IsActive = true },
                        new AssetDefinition { Symbol = "MSFT", Name = "Microsoft Corp.", Sector = "Technology", Type = "Stock", IsActive = true },
                        new AssetDefinition { Symbol = "GOOGL", Name = "Alphabet Inc.", Sector = "Technology", Type = "Stock", IsActive = true },
                        new AssetDefinition { Symbol = "AMZN", Name = "Amazon.com Inc.", Sector = "Consumer Cyclical", Type = "Stock", IsActive = true },
                        new AssetDefinition { Symbol = "TSLA", Name = "Tesla Inc.", Sector = "Consumer Cyclical", Type = "Stock", IsActive = true },
                        new AssetDefinition { Symbol = "NVDA", Name = "NVIDIA Corp.", Sector = "Technology", Type = "Stock", IsActive = true },
                        new AssetDefinition { Symbol = "META", Name = "Meta Platforms Inc.", Sector = "Technology", Type = "Stock", IsActive = true },
                        new AssetDefinition { Symbol = "NFLX", Name = "Netflix Inc.", Sector = "Communication Services", Type = "Stock", IsActive = true },
                        new AssetDefinition { Symbol = "BTC-USD", Name = "Bitcoin USD", Sector = "Crypto", Type = "Crypto", IsActive = true },
                        new AssetDefinition { Symbol = "ETH-USD", Name = "Ethereum USD", Sector = "Crypto", Type = "Crypto", IsActive = true }
                    };

                    await _mongoContext.Assets.InsertManyAsync(initialAssets, cancellationToken: cancellationToken);
                    _logger.LogInformation($"Seeded {initialAssets.Count} assets.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error seeding data.");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

