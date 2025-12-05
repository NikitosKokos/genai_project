using FinancialAdvisor.Application.Interfaces;
using FinancialAdvisor.Application.Models;
using FinancialAdvisor.Infrastructure.Data;
using Microsoft.Extensions.Logging;
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
        private readonly ILogger<MarketDataService> _logger;
        
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
            // Crypto mappings - accept simple symbols like BTC, ETH
            { "btc", "BTC-USD" },
            { "bitcoin", "BTC-USD" },
            { "eth", "ETH-USD" },
            { "ethereum", "ETH-USD" }
        };

        public MarketDataService(MongoDbContext mongoContext, HttpClient httpClient, ILogger<MarketDataService> logger)
        {
            _mongoContext = mongoContext;
            _httpClient = httpClient;
            _logger = logger;
            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            }
        }

        public async Task<List<MarketDataCache>> GetMarketDataAsync(List<string> symbols)
        {
            if (symbols == null || !symbols.Any())
                return new List<MarketDataCache>();

            var distinctSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            foreach (var s in symbols)
            {
                if (string.IsNullOrWhiteSpace(s))
                    continue;

                var clean = s.Trim().Replace("?", "").Replace(",", "").Replace(".", "");
                
                // Check common ticker mappings first (includes crypto: BTC->BTC-USD, ETH->ETH-USD)
                if (_commonTickers.TryGetValue(clean, out var ticker))
                {
                    distinctSymbols.Add(ticker);
                }
                // Handle crypto symbols with -USD suffix (e.g., BTC-USD, ETH-USD) - accept as-is
                else if (clean.Contains("-USD"))
                {
                    distinctSymbols.Add(clean.ToUpperInvariant());
                }
                // Handle regular stock symbols (up to 5 letters)
                else if (clean.Length <= 5 && clean.All(char.IsLetter))
                {
                    distinctSymbols.Add(clean.ToUpperInvariant());
                }
                // Fallback: use as-is if valid
                else if (clean.Length > 0)
                {
                    distinctSymbols.Add(clean.ToUpperInvariant());
                }
            }

            if (!distinctSymbols.Any())
                return new List<MarketDataCache>();

            var result = new List<MarketDataCache>();
            var symbolsToFetch = distinctSymbols.ToList();

            foreach (var symbol in symbolsToFetch)
            {
                try
                {
                    // Check cache first (if data is less than 5 minutes old, use cached value)
                    var cachedData = await GetCachedDataAsync(symbol);
                    if (cachedData != null)
                    {
                        var cacheAgeMinutes = (DateTime.UtcNow - cachedData.LastUpdated).TotalMinutes;
                        if (cacheAgeMinutes < 5)
                        {
                            _logger.LogInformation("Using cached data for {Symbol} (age: {AgeMinutes:F1} minutes, price: ${Price:F2})", 
                                symbol, cacheAgeMinutes, cachedData.Price);
                            result.Add(cachedData);
                            continue;
                        }
                        else
                        {
                            _logger.LogInformation("Cache expired for {Symbol} (age: {AgeMinutes:F1} minutes), fetching fresh data", 
                                symbol, cacheAgeMinutes);
                        }
                    }
                    else
                    {
                        _logger.LogInformation("No cache found for {Symbol}, fetching from API", symbol);
                    }

                    // Fetch fresh data from Yahoo Finance API
                    // URL encode the symbol to handle special characters (e.g., BTC-USD)
                    var encodedSymbol = Uri.EscapeDataString(symbol);
                    var url = $"https://query2.finance.yahoo.com/v8/finance/chart/{encodedSymbol}?interval=1d&range=1d";
                    
                    _logger.LogDebug("Fetching market data for symbol: {Symbol} from URL: {Url}", symbol, url);
                    
                    var response = await _httpClient.GetAsync(url);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        
                        // Log raw response for debugging (first 500 chars for crypto symbols)
                        if (symbol.Contains("-USD", StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogDebug("Crypto API response for {Symbol} (first 500 chars): {Response}", 
                                symbol, json.Length > 500 ? json.Substring(0, 500) : json);
                        }
                        
                        using var doc = JsonDocument.Parse(json);
                        
                        // Check if result array exists and has data
                        if (!doc.RootElement.TryGetProperty("chart", out var chartProp))
                        {
                            _logger.LogWarning("No 'chart' property in API response for symbol: {Symbol}", symbol);
                            continue;
                        }
                        
                        if (!chartProp.TryGetProperty("result", out var resultProp))
                        {
                            _logger.LogWarning("No 'result' property in API response for symbol: {Symbol}", symbol);
                            continue;
                        }
                        
                        if (resultProp.GetArrayLength() == 0)
                        {
                            _logger.LogWarning("Empty result array in API response for symbol: {Symbol}", symbol);
                            continue;
                        }

                        var resultElem = resultProp[0];
                        if (!resultElem.TryGetProperty("meta", out var meta))
                        {
                            _logger.LogWarning("No 'meta' property in API response for symbol: {Symbol}", symbol);
                            continue;
                        }

                        // Check for errors in the response
                        if (resultElem.TryGetProperty("error", out var errorProp))
                        {
                            var errorMessage = errorProp.GetRawText();
                            _logger.LogWarning("API returned error for symbol {Symbol}: {Error}", symbol, errorMessage);
                            continue;
                        }

                        decimal price = 0;
                        decimal prevClose = 0;
                        long volume = 0;
                        bool isCrypto = symbol.Contains("-USD", StringComparison.OrdinalIgnoreCase);

                        // Try multiple price fields (crypto and stocks may use different fields)
                        // Priority: regularMarketPrice > currentPrice > previousClose
                        if (meta.TryGetProperty("regularMarketPrice", out var priceProp))
                        {
                            price = priceProp.GetDecimal();
                        }
                        else if (meta.TryGetProperty("currentPrice", out var currentPriceProp))
                        {
                            price = currentPriceProp.GetDecimal();
                        }
                        else if (meta.TryGetProperty("previousClose", out var prevPriceProp))
                        {
                            price = prevPriceProp.GetDecimal();
                        }
                        else if (meta.TryGetProperty("regularMarketPreviousClose", out var regPrevCloseProp))
                        {
                            price = regPrevCloseProp.GetDecimal();
                        }
                        
                        // If still no price, try to get from indicators/quote (for crypto)
                        if (price == 0 && resultElem.TryGetProperty("indicators", out var indicators))
                        {
                            if (indicators.TryGetProperty("quote", out var quote))
                            {
                                var quoteArray = quote.EnumerateArray().FirstOrDefault();
                                if (quoteArray.ValueKind != JsonValueKind.Undefined)
                                {
                                    if (quoteArray.TryGetProperty("close", out var closeArray))
                                    {
                                        var closeValues = closeArray.EnumerateArray()
                                            .Where(v => v.ValueKind == JsonValueKind.Number)
                                            .Select(v => v.GetDecimal())
                                            .Where(v => v > 0)
                                            .ToList();
                                        
                                        if (closeValues.Any())
                                        {
                                            price = closeValues.Last(); // Get the latest non-zero close price
                                        }
                                    }
                                }
                            }
                        }
                        
                        // Last resort: use chartPreviousClose if it's crypto
                        if (price == 0 && isCrypto && meta.TryGetProperty("chartPreviousClose", out var cryptoPriceProp))
                        {
                            price = cryptoPriceProp.GetDecimal();
                        }

                        // Get previous close for change calculation
                        if (meta.TryGetProperty("chartPreviousClose", out var closeProp))
                        {
                            prevClose = closeProp.GetDecimal();
                        }
                        else if (meta.TryGetProperty("previousClose", out var prevCloseProp))
                        {
                            prevClose = prevCloseProp.GetDecimal();
                        }
                        else if (meta.TryGetProperty("regularMarketPreviousClose", out var regPrevClose))
                        {
                            prevClose = regPrevClose.GetDecimal();
                        }

                        // For crypto, if we don't have prevClose, use current price (no change)
                        if (prevClose == 0 && price > 0)
                        {
                            prevClose = price;
                        }

                        // Get volume (may not be available for crypto)
                        if (meta.TryGetProperty("regularMarketVolume", out var volumeProp))
                        {
                            volume = volumeProp.GetInt64();
                        }
                        else if (meta.TryGetProperty("volume24Hr", out var volume24Prop))
                        {
                            // Crypto sometimes uses volume24Hr
                            volume = volume24Prop.GetInt64();
                        }

                        decimal changePercent = (prevClose > 0) ? ((price - prevClose) / prevClose) * 100 : 0;
                        
                        // Validate price is reasonable (not zero, not negative, not absurdly large)
                        if (price <= 0 || price > 1000000000) // Max $1B per share/coin
                        {
                            _logger.LogWarning("Invalid price {Price} for symbol: {Symbol}. Skipping cache update.", price, symbol);
                            continue;
                        }
                        
                        // Validate change percent is reasonable (not more than 1000% change)
                        if (Math.Abs(changePercent) > 1000)
                        {
                            _logger.LogWarning("Unusual change percent {ChangePercent}% for symbol: {Symbol}. Using 0% instead.", 
                                changePercent, symbol);
                            changePercent = 0;
                        }

                        // Check if we have existing cached data to preserve the _id
                        var existingCache = await GetCachedDataAsync(symbol);
                        
                        var data = new MarketDataCache
                        {
                            Id = existingCache?.Id ?? MongoDB.Bson.ObjectId.Empty, // Preserve existing _id or use Empty (MongoDB will generate)
                            Symbol = symbol,
                            Price = price,
                            Volume = volume,
                            ChangePercent = changePercent,
                            LastUpdated = DateTime.UtcNow
                        };
                        
                        result.Add(data);
                        // Update cache - await to ensure it completes
                        await UpdateCacheAsync(data, existingCache);
                        
                        _logger.LogInformation("Successfully fetched and cached market data for {Symbol}: Price=${Price:F2}, Change={ChangePercent:F2}%, Volume={Volume}", 
                            symbol, price, changePercent, volume);
                    }
                    else
                    {
                        _logger.LogWarning("API request failed for symbol {Symbol}: Status {StatusCode}", 
                            symbol, response.StatusCode);
                        
                        // If API fails, try to use cached data even if old
                        if (cachedData != null)
                        {
                            _logger.LogInformation("Using stale cached data for symbol: {Symbol}", symbol);
                            result.Add(cachedData);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception while fetching market data for symbol: {Symbol}", symbol);
                    
                    // Try to use cached data if available
                    var cachedData = await GetCachedDataAsync(symbol);
                    if (cachedData != null)
                    {
                        _logger.LogInformation("Using cached data as fallback for symbol: {Symbol}", symbol);
                        result.Add(cachedData);
                    }
                }
            }

            return result;
        }
        
        private async Task<MarketDataCache> GetCachedDataAsync(string symbol)
        {
            try
            {
                var filter = Builders<MarketDataCache>.Filter.Eq(x => x.Symbol, symbol);
                var cached = await _mongoContext.MarketCache.Find(filter).FirstOrDefaultAsync();
                if (cached != null)
                {
                    _logger.LogDebug("Cache hit for {Symbol} (last updated: {LastUpdated})", symbol, cached.LastUpdated);
                }
                else
                {
                    _logger.LogDebug("Cache miss for {Symbol}", symbol);
                }
                return cached;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading cache for {Symbol}", symbol);
                return null;
            }
        }

        private async Task UpdateCacheAsync(MarketDataCache data, MarketDataCache? existingCache = null)
        {
            try 
            {
                var filter = Builders<MarketDataCache>.Filter.Eq(x => x.Symbol, data.Symbol);
                
                // Use UpdateOneAsync with Set operations - this won't touch the _id field
                var update = Builders<MarketDataCache>.Update
                    .Set(x => x.Price, data.Price)
                    .Set(x => x.Volume, data.Volume)
                    .Set(x => x.ChangePercent, data.ChangePercent)
                    .Set(x => x.LastUpdated, data.LastUpdated);
                
                var result = await _mongoContext.MarketCache.UpdateOneAsync(
                    filter, 
                    update, 
                    new UpdateOptions { IsUpsert = true });
                
                if (result.UpsertedId != null)
                {
                    _logger.LogInformation("✅ Inserted new cache entry for {Symbol}: Price=${Price:F2}, Change={ChangePercent:F2}%", 
                        data.Symbol, data.Price, data.ChangePercent);
                }
                else if (result.ModifiedCount > 0)
                {
                    _logger.LogInformation("✅ Updated cache entry for {Symbol}: Price=${Price:F2}, Change={ChangePercent:F2}%", 
                        data.Symbol, data.Price, data.ChangePercent);
                }
                else
                {
                    _logger.LogWarning("⚠️ Cache update for {Symbol} completed but no document was modified or inserted", data.Symbol);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to update cache for symbol: {Symbol}. Error: {ErrorMessage}", 
                    data.Symbol, ex.Message);
                // Don't throw - we still want to return the data even if cache update fails
            }
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