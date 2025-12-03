using FinancialAdvisor.Application.Interfaces;
using FinancialAdvisor.Application.Models;
using FinancialAdvisor.Infrastructure.Data;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using YahooFinanceApi;

namespace FinancialAdvisor.Infrastructure.Services
{
    public class MarketDataService : IMarketDataService
    {
        private readonly MongoDbContext _mongoContext;
        
        // Simple mapping for demo purposes.
        private static readonly Dictionary<string, string> _commonTickers = new(StringComparer.OrdinalIgnoreCase)
        {
            { "apple", "AAPL" },
            { "microsoft", "MSFT" },
            { "google", "GOOGL" },
            { "amazon", "AMZN" },
            { "tesla", "TSLA" },
            { "nvidia", "NVDA" },
            { "meta", "META" },
            { "facebook", "META" },
            { "bitcoin", "BTC-USD" },
            { "ethereum", "ETH-USD" }
        };

        public MarketDataService(MongoDbContext mongoContext)
        {
            _mongoContext = mongoContext;
        }

        public async Task<List<MarketDataCache>> GetMarketDataAsync(List<string> symbols)
        {
            var distinctSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            foreach (var s in symbols)
            {
                var clean = s.Trim();
                // Remove punctuation if any
                clean = clean.Replace("?", "").Replace(",", "").Replace(".", "");

                if (_commonTickers.TryGetValue(clean, out var ticker))
                    distinctSymbols.Add(ticker);
                else if (clean.Length <= 5 && clean.All(char.IsLetter))
                     // Assume it's a ticker if short and letters
                    distinctSymbols.Add(clean.ToUpperInvariant());
            }

            if (!distinctSymbols.Any())
                return new List<MarketDataCache>();

            var result = new List<MarketDataCache>();
            var symbolsToFetch = distinctSymbols.ToList();

            try
            {
                // Fetch from Yahoo Finance
                var quotes = await Yahoo.Symbols(symbolsToFetch.ToArray())
                    .Fields(Field.Symbol, Field.RegularMarketPrice, Field.RegularMarketChangePercent, Field.RegularMarketVolume)
                    .QueryAsync();
                
                foreach (var quote in quotes)
                {
                    var data = new MarketDataCache
                    {
                        Symbol = quote.Key,
                        Price = (decimal)quote.Value.RegularMarketPrice,
                        ChangePercent = (decimal)quote.Value.RegularMarketChangePercent,
                        Volume = (long)quote.Value.RegularMarketVolume,
                        LastUpdated = DateTime.UtcNow
                    };
                    result.Add(data);
                    
                    // Fire and forget: Update cache
                    _ = UpdateCacheAsync(data);
                }
            }
            catch
            {
                // Fallback to DB if API fails or no internet
                try 
                {
                    var cached = await _mongoContext.MarketCache
                        .Find(m => symbolsToFetch.Contains(m.Symbol))
                        .ToListAsync();
                    result.AddRange(cached);
                }
                catch { /* ignore db errors */ }
            }

            return result;
        }
        
        private async Task UpdateCacheAsync(MarketDataCache data)
        {
            try 
            {
                var filter = Builders<MarketDataCache>.Filter.Eq(x => x.Symbol, data.Symbol);
                await _mongoContext.MarketCache.ReplaceOneAsync(filter, data, new ReplaceOptions { IsUpsert = true });
            }
            catch { /* ignore cache errors */ }
        }

        public string FormatMarketContext(List<MarketDataCache> marketData)
        {
            if (!marketData.Any())
                return "No real-time market data available.";

            var prices = marketData
                .Select(m => $"- {m.Symbol}: ${m.Price:F2} ({m.ChangePercent:+0.00;-0.00}%)")
                .ToList();

            return $@"REAL-TIME MARKET PRICES (Yahoo Finance):
{string.Join("\n", prices)}";
        }
    }
}