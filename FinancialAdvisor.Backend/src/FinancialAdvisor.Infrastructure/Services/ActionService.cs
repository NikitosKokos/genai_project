using FinancialAdvisor.Application.Interfaces;
using FinancialAdvisor.Application.Models;
using FinancialAdvisor.Infrastructure.Data;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace FinancialAdvisor.Infrastructure.Services
{
    public class ActionService : IActionService
    {
        private readonly MongoDbContext _mongoContext;
        private readonly ILogger<ActionService> _logger;

        public ActionService(MongoDbContext mongoContext, ILogger<ActionService> logger)
        {
            _mongoContext = mongoContext;
            _logger = logger;
        }

        public async Task<List<Trade>> ParseAndExecuteTradesAsync(string llmResponse, string sessionId)
        {
            var trades = ParseTradesFromResponse(llmResponse);
            foreach (var trade in trades)
            {
                await ExecuteMockTradeAsync(sessionId, trade);
            }
            return trades;
        }

        private List<Trade> ParseTradesFromResponse(string response)
        {
            var trades = new List<Trade>();
            try
            {
                var startIndex = response.IndexOf("{");
                var endIndex = response.LastIndexOf("}");
                if (startIndex >= 0 && endIndex > startIndex)
                {
                    var json = response.Substring(startIndex, endIndex - startIndex + 1);
                    
                    using (var doc = JsonDocument.Parse(json))
                    {
                        if (doc.RootElement.TryGetProperty("trades", out var tradesElement) && tradesElement.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var element in tradesElement.EnumerateArray())
                            {
                                var trade = new Trade
                                {
                                    Symbol = element.GetProperty("symbol").GetString(),
                                    Action = element.GetProperty("action").GetString(),
                                    Quantity = element.GetProperty("qty").GetInt32(),
                                    Price = 0, 
                                    ExecutedAt = DateTime.UtcNow
                                };
                                trades.Add(trade);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse trades from LLM response");
            }
            return trades;
        }

        private async Task ExecuteMockTradeAsync(string sessionId, Trade trade)
        {
            var tradingHistory = await _mongoContext.TradingHistory
                .Find(t => t.SessionId == sessionId)
                .FirstOrDefaultAsync();

            if (tradingHistory == null)
            {
                tradingHistory = new TradingHistory
                {
                    SessionId = sessionId,
                    Trades = new List<Trade> { trade }
                };
                await _mongoContext.TradingHistory.InsertOneAsync(tradingHistory);
            }
            else
            {
                var update = Builders<TradingHistory>.Update.Push(t => t.Trades, trade);
                await _mongoContext.TradingHistory.UpdateOneAsync(
                    t => t.SessionId == sessionId,
                    update
                );
            }

            _logger.LogInformation($"[{sessionId}] Executed mock trade: {trade.Action} {trade.Quantity} {trade.Symbol}");
        }
    }
}
