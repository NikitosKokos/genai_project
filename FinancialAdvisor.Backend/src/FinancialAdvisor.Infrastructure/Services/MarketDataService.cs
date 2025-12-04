using FinancialAdvisor.Application.Interfaces;
using FinancialAdvisor.Application.Models;
using FinancialAdvisor.Infrastructure.Data;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace FinancialAdvisor.Infrastructure.Services
{
    public class MarketDataService : IMarketDataService
    {
        private readonly MongoDbContext _mongoContext;
        private readonly HttpClient _httpClient;
        
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

        public MarketDataService(MongoDbContext mongoContext, HttpClient httpClient)
        {
            _mongoContext = mongoContext;
            _httpClient = httpClient;
            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            }
        }

        public async Task<List<MarketDataCache>> GetMarketDataAsync(List<string> symbols)
        {
            var distinctSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            foreach (var s in symbols)
            {
                var clean = s.Trim().Replace("?", "").Replace(",", "").Replace(".", "");
                if (_commonTickers.TryGetValue(clean, out var ticker))
                    distinctSymbols.Add(ticker);
                else if (clean.Length <= 5 && clean.All(char.IsLetter))
                    distinctSymbols.Add(clean.ToUpperInvariant());
            }

            if (!distinctSymbols.Any())
                return new List<MarketDataCache>();

            var result = new List<MarketDataCache>();
            var symbolsToFetch = distinctSymbols.ToList();

            foreach (var symbol in symbolsToFetch)
            {
                try
                {
                    // Using query2 as alternative
                    var url = $"https://query2.finance.yahoo.com/v8/finance/chart/{symbol}?interval=1d&range=1d";
                    var response = await _httpClient.GetAsync(url);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(json);
                        var resultElem = doc.RootElement.GetProperty("chart").GetProperty("result")[0];
                        var meta = resultElem.GetProperty("meta");

                        decimal price = 0;
                        decimal prevClose = 0;

                        if (meta.TryGetProperty("regularMarketPrice", out var priceProp))
                            price = priceProp.GetDecimal();
                        
                        if (meta.TryGetProperty("chartPreviousClose", out var closeProp))
                            prevClose = closeProp.GetDecimal();

                        decimal changePercent = (prevClose > 0) ? ((price - prevClose) / prevClose) * 100 : 0;

                        var data = new MarketDataCache
                        {
                            Symbol = symbol,
                            Price = price,
                            ChangePercent = changePercent,
                            LastUpdated = DateTime.UtcNow
                        };
                        
                        result.Add(data);
                        // Write-only cache update
                        _ = UpdateCacheAsync(data);
                    }
                    else
                    {
                        Console.WriteLine($"[MarketDataService] API Error {response.StatusCode} for {symbol}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MarketDataService] Exception for {symbol}: {ex.Message}");
                }
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
            catch { }
        }

        public string FormatMarketContext(List<MarketDataCache> marketData)
        {
            if (!marketData.Any())
                return "No real-time market data available.";

            var prices = marketData
                .Select(m => $"- {m.Symbol}: ${m.Price:F2} ({m.ChangePercent:+0.00;-0.00}%)")
                .ToList();

            return $@"REAL-TIME MARKET PRICES:
{string.Join("\n", prices)}";
        }
    }
}