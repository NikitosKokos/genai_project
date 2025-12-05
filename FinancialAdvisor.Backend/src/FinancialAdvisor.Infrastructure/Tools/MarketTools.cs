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
        public string Description => "Get current stock price. Args: symbol (string)";

        public async Task<string> ExecuteAsync(string argsJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(argsJson);
                if (!doc.RootElement.TryGetProperty("symbol", out var symbolProp))
                    return JsonSerializer.Serialize(new { error = "Missing symbol argument" });

                var symbol = symbolProp.GetString();
                var data = await _marketDataService.GetMarketDataAsync(new List<string> { symbol });
                var marketData = data.FirstOrDefault();

                if (marketData == null)
                    return JsonSerializer.Serialize(new { error = $"Symbol {symbol} not found" });

                return JsonSerializer.Serialize(new
                {
                    symbol = marketData.Symbol,
                    price = marketData.Price,
                    currency = "USD",
                    timestamp = marketData.LastUpdated.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    source = "market-api",
                    summary = $"{marketData.Symbol} is currently trading at ${marketData.Price:F2}."
                });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { error = ex.Message });
            }
        }
    }
}
