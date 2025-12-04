using System;
using System.Text.Json;
using System.Threading.Tasks;
using FinancialAdvisor.Application.Interfaces;

namespace FinancialAdvisor.Infrastructure.Tools
{
    public class BuyStockTool : ITool
    {
        public string Name => "buy_stock";
        public string Description => "Place a buy order. Args: symbol (string), qty (int)";

        public Task<string> ExecuteAsync(string argsJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(argsJson);
                var symbol = doc.RootElement.GetProperty("symbol").GetString();
                var qty = doc.RootElement.GetProperty("qty").GetInt32();

                // MVP Mock Execution
                var result = new
                {
                    status = "ok",
                    order_id = $"o-{Guid.NewGuid().ToString().Substring(0, 8)}",
                    executed_qty = qty,
                    avg_price = 150.00, // Mock price
                    timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                };

                return Task.FromResult(JsonSerializer.Serialize(result));
            }
            catch (Exception ex)
            {
                return Task.FromResult(JsonSerializer.Serialize(new { error = ex.Message }));
            }
        }
    }

    public class SellStockTool : ITool
    {
        public string Name => "sell_stock";
        public string Description => "Place a sell order. Args: symbol (string), qty (int)";

        public Task<string> ExecuteAsync(string argsJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(argsJson);
                var symbol = doc.RootElement.GetProperty("symbol").GetString();
                var qty = doc.RootElement.GetProperty("qty").GetInt32();

                // MVP Mock Execution
                var result = new
                {
                    status = "ok",
                    order_id = $"o-{Guid.NewGuid().ToString().Substring(0, 8)}",
                    executed_qty = qty,
                    avg_price = 150.00, // Mock price
                    timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                };

                return Task.FromResult(JsonSerializer.Serialize(result));
            }
            catch (Exception ex)
            {
                return Task.FromResult(JsonSerializer.Serialize(new { error = ex.Message }));
            }
        }
    }
}

