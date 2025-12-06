using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FinancialAdvisor.Application.Interfaces;
using FinancialAdvisor.Application.Models;
using Microsoft.Extensions.Logging;

namespace FinancialAdvisor.Infrastructure.Tools
{
    public class BuyStockTool : ITool
    {
        private readonly IContextService _contextService;
        private readonly IMarketDataService _marketDataService;
        private readonly ILogger<BuyStockTool> _logger;

        public BuyStockTool(IContextService contextService, IMarketDataService marketDataService, ILogger<BuyStockTool> logger)
        {
            _contextService = contextService;
            _marketDataService = marketDataService;
            _logger = logger;
        }

        public string Name => "buy_stock";
        public string Description => "Execute a stock purchase. Args: symbol (string), qty (int), user_id (string)";

        public async Task<string> ExecuteAsync(string argsJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(argsJson);
                var symbol = doc.RootElement.GetProperty("symbol").GetString();
                var qty = doc.RootElement.GetProperty("qty").GetInt32();
                var userId = doc.RootElement.TryGetProperty("user_id", out var userIdProp) 
                    ? userIdProp.GetString() 
                    : null;

                if (string.IsNullOrWhiteSpace(symbol) || qty <= 0)
                {
                    return JsonSerializer.Serialize(new { error = "Invalid symbol or quantity" });
                }

                // Normalize symbol (e.g., "apple" -> "AAPL")
                var normalizedSymbol = NormalizeSymbol(symbol);
                
                // Get current price
                var marketData = await _marketDataService.GetMarketDataAsync(new List<string> { normalizedSymbol });
                if (!marketData.Any())
                {
                    return JsonSerializer.Serialize(new { error = $"Could not fetch price for {symbol}" });
                }

                var currentPrice = marketData.First().Price;
                var totalCost = currentPrice * qty;

                // Get portfolio to check cash balance
                var portfolio = await _contextService.GetPortfolioAsync(userId ?? "demo_session_001");
                if (portfolio == null)
                {
                    return JsonSerializer.Serialize(new { error = "Portfolio not found" });
                }

                // Check if user has enough cash
                if (portfolio.CashBalance < totalCost)
                {
                    return JsonSerializer.Serialize(new 
                    { 
                        error = $"Insufficient funds. Required: ${totalCost:N2}, Available: ${portfolio.CashBalance:N2}" 
                    });
                }

                // Execute trade: update portfolio
                await _contextService.ExecuteBuyTradeAsync(userId ?? "demo_session_001", normalizedSymbol, qty, currentPrice);

                // Record trade in history
                var trade = new Trade
                {
                    Symbol = normalizedSymbol,
                    Action = "BUY",
                    Quantity = qty,
                    Price = currentPrice,
                    TotalAmount = totalCost,
                    ExecutedAt = DateTime.UtcNow,
                    Reasoning = $"User requested purchase of {qty} shares of {symbol}"
                };
                await _contextService.RecordTradeAsync(userId ?? "demo_session_001", trade);

                _logger.LogInformation($"Executed BUY: {qty} {normalizedSymbol} @ ${currentPrice:N2} = ${totalCost:N2}");

                return JsonSerializer.Serialize(new
                {
                    status = "ok",
                    order_id = $"o-{Guid.NewGuid().ToString().Substring(0, 8)}",
                    symbol = normalizedSymbol,
                    executed_qty = qty,
                    avg_price = currentPrice,
                    total_cost = totalCost,
                    timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing buy trade");
                return JsonSerializer.Serialize(new { error = ex.Message });
            }
        }

        private string NormalizeSymbol(string symbol)
        {
            var clean = symbol.Trim().ToUpperInvariant();
            // Handle common mappings
            var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "APPLE", "AAPL" },
                { "MICROSOFT", "MSFT" },
                { "GOOGLE", "GOOGL" },
                { "AMAZON", "AMZN" },
                { "TESLA", "TSLA" },
                { "NVidia", "NVDA" },
                { "META", "META" },
                { "FACEBOOK", "META" }
            };
            return mappings.TryGetValue(clean, out var mapped) ? mapped : clean;
        }
    }

    public class SellStockTool : ITool
    {
        private readonly IContextService _contextService;
        private readonly IMarketDataService _marketDataService;
        private readonly ILogger<SellStockTool> _logger;

        public SellStockTool(IContextService contextService, IMarketDataService marketDataService, ILogger<SellStockTool> logger)
        {
            _contextService = contextService;
            _marketDataService = marketDataService;
            _logger = logger;
        }

        public string Name => "sell_stock";
        public string Description => "Execute a stock sale. Args: symbol (string), qty (int), user_id (string)";

        public async Task<string> ExecuteAsync(string argsJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(argsJson);
                var symbol = doc.RootElement.GetProperty("symbol").GetString();
                var qty = doc.RootElement.GetProperty("qty").GetInt32();
                var userId = doc.RootElement.TryGetProperty("user_id", out var userIdProp) 
                    ? userIdProp.GetString() 
                    : null;

                if (string.IsNullOrWhiteSpace(symbol) || qty <= 0)
                {
                    return JsonSerializer.Serialize(new { error = "Invalid symbol or quantity" });
                }

                // Normalize symbol
                var normalizedSymbol = NormalizeSymbol(symbol);
                
                // Get current price
                var marketData = await _marketDataService.GetMarketDataAsync(new List<string> { normalizedSymbol });
                if (!marketData.Any())
                {
                    return JsonSerializer.Serialize(new { error = $"Could not fetch price for {symbol}" });
                }

                var currentPrice = marketData.First().Price;
                var totalProceeds = currentPrice * qty;

                // Get portfolio to check holdings
                var portfolio = await _contextService.GetPortfolioAsync(userId ?? "demo_session_001");
                if (portfolio == null)
                {
                    return JsonSerializer.Serialize(new { error = "Portfolio not found" });
                }

                // Check if user has enough shares
                var holding = portfolio.Holdings?.FirstOrDefault(h => 
                    string.Equals(h.Symbol, normalizedSymbol, StringComparison.OrdinalIgnoreCase));
                
                if (holding == null || holding.Quantity < qty)
                {
                    var available = holding?.Quantity ?? 0;
                    return JsonSerializer.Serialize(new 
                    { 
                        error = $"Insufficient shares. Requested: {qty}, Available: {available}" 
                    });
                }

                // Execute trade: update portfolio
                await _contextService.ExecuteSellTradeAsync(userId ?? "demo_session_001", normalizedSymbol, qty, currentPrice);

                // Record trade in history
                var trade = new Trade
                {
                    Symbol = normalizedSymbol,
                    Action = "SELL",
                    Quantity = qty,
                    Price = currentPrice,
                    TotalAmount = totalProceeds,
                    ExecutedAt = DateTime.UtcNow,
                    Reasoning = $"User requested sale of {qty} shares of {symbol}"
                };
                await _contextService.RecordTradeAsync(userId ?? "demo_session_001", trade);

                _logger.LogInformation($"Executed SELL: {qty} {normalizedSymbol} @ ${currentPrice:N2} = ${totalProceeds:N2}");

                return JsonSerializer.Serialize(new
                {
                    status = "ok",
                    order_id = $"o-{Guid.NewGuid().ToString().Substring(0, 8)}",
                    symbol = normalizedSymbol,
                    executed_qty = qty,
                    avg_price = currentPrice,
                    total_proceeds = totalProceeds,
                    timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing sell trade");
                return JsonSerializer.Serialize(new { error = ex.Message });
            }
        }

        private string NormalizeSymbol(string symbol)
        {
            var clean = symbol.Trim().ToUpperInvariant();
            var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "APPLE", "AAPL" },
                { "MICROSOFT", "MSFT" },
                { "GOOGLE", "GOOGL" },
                { "AMAZON", "AMZN" },
                { "TESLA", "TSLA" },
                { "NVidia", "NVDA" },
                { "META", "META" },
                { "FACEBOOK", "META" }
            };
            return mappings.TryGetValue(clean, out var mapped) ? mapped : clean;
        }
    }
}

