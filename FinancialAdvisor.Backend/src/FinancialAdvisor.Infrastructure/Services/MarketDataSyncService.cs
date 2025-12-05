using FinancialAdvisor.Application.Interfaces;
using FinancialAdvisor.Application.Models;
using FinancialAdvisor.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FinancialAdvisor.Infrastructure.Services
{
    /// <summary>
    /// Background service that syncs all active assets from the assets collection
    /// to the market_cache collection, ensuring all assets have market data available.
    /// </summary>
    public class MarketDataSyncService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MarketDataSyncService> _logger;
        private readonly TimeSpan _syncInterval = TimeSpan.FromMinutes(15); // Sync every 15 minutes
        private readonly TimeSpan _initialDelay = TimeSpan.FromSeconds(30); // Wait 30 seconds on startup

        public MarketDataSyncService(
            IServiceProvider serviceProvider,
            ILogger<MarketDataSyncService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Market Data Sync Service is starting.");

            // Wait for initial delay to allow other services to initialize
            await Task.Delay(_initialDelay, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await SyncMarketDataAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during market data sync.");
                }

                // Wait for next sync cycle
                await Task.Delay(_syncInterval, stoppingToken);
            }
        }

        private async Task SyncMarketDataAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var mongoContext = scope.ServiceProvider.GetRequiredService<MongoDbContext>();
            var marketDataService = scope.ServiceProvider.GetRequiredService<IMarketDataService>();

            try
            {
                _logger.LogInformation("Starting market data sync for all active assets...");

                // Get all active assets from the assets collection
                var activeAssets = await mongoContext.Assets
                    .Find(a => a.IsActive)
                    .ToListAsync(cancellationToken);

                if (!activeAssets.Any())
                {
                    _logger.LogWarning("No active assets found in the assets collection.");
                    return;
                }

                _logger.LogInformation("Found {Count} active assets to sync.", activeAssets.Count);

                // Get all symbols from active assets
                var symbols = activeAssets
                    .Where(a => !string.IsNullOrWhiteSpace(a.Symbol))
                    .Select(a => a.Symbol!)
                    .ToList();

                if (!symbols.Any())
                {
                    _logger.LogWarning("No valid symbols found in active assets.");
                    return;
                }

                _logger.LogInformation("Syncing market data for symbols: {Symbols}", string.Join(", ", symbols));

                // Fetch market data for all symbols
                var marketData = await marketDataService.GetMarketDataAsync(symbols);

                // Log results
                var successCount = marketData?.Count ?? 0;
                var failedCount = symbols.Count - successCount;

                if (successCount > 0)
                {
                    _logger.LogInformation(
                        "Market data sync completed: {SuccessCount} assets updated successfully.",
                        successCount);
                }

                if (failedCount > 0 && marketData != null)
                {
                    var successfulSymbols = marketData
                        .Where(m => !string.IsNullOrWhiteSpace(m.Symbol))
                        .Select(m => m.Symbol!)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    
                    var failedSymbols = symbols
                        .Where(s => !successfulSymbols.Contains(s))
                        .ToList();
                    
                    _logger.LogWarning(
                        "Market data sync: {FailedCount} assets failed to update. Symbols: {FailedSymbols}",
                        failedCount,
                        string.Join(", ", failedSymbols));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync market data.");
                throw;
            }
        }
    }
}

