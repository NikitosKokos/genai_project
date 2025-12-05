using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using FinancialAdvisor.Application.Interfaces;
using FinancialAdvisor.Application.Models;

namespace FinancialAdvisor.Infrastructure.Tools
{
    public class GetStockPriceTool : ITool
    {
        private readonly IMarketDataService _marketDataService;

        public GetStockPriceTool(IMarketDataService marketDataService)
        {
            _marketDataService = marketDataService;
        }

        public string Name => "get_stock_price";
        public string Description => "Get current stock or crypto price. Args: symbol (string) - For stocks use ticker (e.g., AAPL, MSFT). For crypto use simple symbol (e.g., BTC for Bitcoin, ETH for Ethereum). Examples: 'AAPL', 'BTC', 'ETH'.";

        private string NormalizeSymbol(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                return symbol;

            var normalized = symbol.Trim().ToUpperInvariant();

            // If already has -USD suffix, return as-is (supports both formats)
            if (normalized.Contains("-USD"))
            {
                return normalized;
            }

            // MarketDataService handles crypto normalization (BTC->BTC-USD, ETH->ETH-USD)
            // So we can pass simple symbols directly
            return normalized;
        }

        public async Task<string> ExecuteAsync(string argsJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(argsJson);
                if (!doc.RootElement.TryGetProperty("symbol", out var symbolProp))
                    return JsonSerializer.Serialize(new { error = "Missing symbol argument" });

                var symbol = symbolProp.GetString();
                if (string.IsNullOrWhiteSpace(symbol))
                    return JsonSerializer.Serialize(new { error = "Symbol cannot be empty" });

                // Normalize symbol (e.g., ETH -> ETH-USD, BTC -> BTC-USD)
                var normalizedSymbol = NormalizeSymbol(symbol);
                var data = await _marketDataService.GetMarketDataAsync(new List<string> { normalizedSymbol });
                var marketData = data.FirstOrDefault();

                if (marketData == null)
                {
                    return JsonSerializer.Serialize(new { error = $"Symbol '{symbol}' not found. For stocks use ticker (e.g., AAPL). For crypto use symbol (e.g., BTC, ETH)." });
                }

                return JsonSerializer.Serialize(new
                {
                    symbol = marketData.Symbol,
                    price = marketData.Price,
                    currency = "USD",
                    timestamp = marketData.LastUpdated.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    source = "market-api"
                });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { error = ex.Message });
            }
        }
    }
}
