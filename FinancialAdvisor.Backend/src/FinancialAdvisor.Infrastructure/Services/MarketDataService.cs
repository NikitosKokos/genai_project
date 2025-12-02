using FinancialAdvisor.Application.Interfaces;
using FinancialAdvisor.Application.Models;
using FinancialAdvisor.Infrastructure.Data;
using MongoDB.Driver;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FinancialAdvisor.Infrastructure.Services
{
    public class MarketDataService : IMarketDataService
    {
        private readonly MongoDbContext _mongoContext;

        public MarketDataService(MongoDbContext mongoContext)
        {
            _mongoContext = mongoContext;
        }

        public async Task<List<MarketDataCache>> GetMarketDataAsync(List<string> symbols)
        {
             if (!symbols.Any())
                return new List<MarketDataCache>();

            var marketData = await _mongoContext.MarketCache
                .Find(m => symbols.Contains(m.Symbol))
                .ToListAsync();

            return marketData;
        }

        public string FormatMarketContext(List<MarketDataCache> marketData)
        {
            if (!marketData.Any())
                return "No market data available.";

            var prices = marketData
                .Select(m => $"- {m.Symbol}: ${m.Price} ({m.ChangePercent:+0.00;-0.00}%)")
                .ToList();

            return $@"Current Market Prices:
{string.Join("\n", prices)}";
        }
    }
}
