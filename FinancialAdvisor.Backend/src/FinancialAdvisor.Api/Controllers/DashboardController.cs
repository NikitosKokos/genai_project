using FinancialAdvisor.Application.Interfaces;
using FinancialAdvisor.Application.Models;
using FinancialAdvisor.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FinancialAdvisor.Api.Controllers
{
    [ApiController]
    [Route("api/dashboard")]
    public class DashboardController : ControllerBase
    {
        private readonly MongoDbContext _mongoContext;
        private readonly IMarketDataService _marketDataService;
        private readonly IContextService _contextService;

        public DashboardController(
            MongoDbContext mongoContext, 
            IMarketDataService marketDataService,
            IContextService contextService)
        {
            _mongoContext = mongoContext;
            _marketDataService = marketDataService;
            _contextService = contextService;
        }

        [HttpGet("news")]
        public async Task<IActionResult> GetNews()
        {
            try 
            {
                var news = await _mongoContext.FinancialDocuments
                    .Find(d => d.Category == "News")
                    .SortByDescending(d => d.CreatedAt)
                    .Limit(10)
                    .ToListAsync();

                return Ok(news.Select(n => new {
                    id = n.Id.ToString(),
                    title = n.Title,
                    summary = n.Content.Length > 100 ? n.Content.Substring(0, 100) + "..." : n.Content,
                    source = n.Source,
                    publishedAt = n.CreatedAt
                }));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("assets")]
        public async Task<IActionResult> GetAssets()
        {
            try
            {
                // 1. Fetch defined assets
                var assets = await _mongoContext.Assets
                    .Find(a => a.IsActive)
                    .ToListAsync();

                if (!assets.Any()) return Ok(new List<object>());

                // 2. Fetch real-time prices
                var symbols = assets.Select(a => a.Symbol).ToList();
                var marketData = await _marketDataService.GetMarketDataAsync(symbols);
                var marketMap = marketData.ToDictionary(m => m.Symbol, m => m, StringComparer.OrdinalIgnoreCase);

                // 3. Merge
                var result = assets.Select(a => {
                    marketMap.TryGetValue(a.Symbol, out var data);
                    return new 
                    {
                        symbol = a.Symbol,
                        name = a.Name,
                        sector = a.Sector,
                        type = a.Type,
                        price = data?.Price ?? 0,
                        changePercent = data?.ChangePercent ?? 0,
                        volume = data?.Volume ?? 0,
                        lastUpdated = data?.LastUpdated ?? DateTime.UtcNow
                    };
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("portfolio")]
        public async Task<IActionResult> GetPortfolio([FromQuery] string sessionId = "default-session")
        {
            try
            {
                // Get current portfolio state
                var portfolio = await _contextService.GetPortfolioAsync(sessionId);
                
                if (portfolio == null)
                {
                    return Ok(new { 
                        totalValue = 0,
                        cashBalance = 100000, // Default starting cash
                        holdings = new List<object>(),
                        performance = new List<object>() // Empty chart
                    });
                }

                // Calculate current value of holdings
                var holdings = new List<object>();
                decimal holdingsValue = 0;

                if (portfolio.Holdings != null && portfolio.Holdings.Any())
                {
                    var symbols = portfolio.Holdings.Select(h => h.Symbol).ToList();
                    var prices = await _marketDataService.GetMarketDataAsync(symbols);
                    var priceMap = prices.ToDictionary(p => p.Symbol, p => p.Price);

                    foreach (var h in portfolio.Holdings)
                    {
                        decimal currentPrice = priceMap.ContainsKey(h.Symbol) ? priceMap[h.Symbol] : h.CurrentPrice;
                        decimal value = h.Quantity * currentPrice;
                        decimal gainLoss = (currentPrice - h.AvgCost) * h.Quantity;
                        decimal gainLossPercent = h.AvgCost > 0 ? ((currentPrice - h.AvgCost) / h.AvgCost) * 100 : 0;

                        holdingsValue += value;

                        holdings.Add(new {
                            symbol = h.Symbol,
                            quantity = h.Quantity,
                            avgCost = h.AvgCost,
                            currentPrice = currentPrice,
                            value = value,
                            gainLoss = gainLoss,
                            gainLossPercent = gainLossPercent
                        });
                    }
                }

                var totalValue = portfolio.CashBalance + holdingsValue;

                return Ok(new {
                    totalValue = totalValue,
                    cashBalance = portfolio.CashBalance,
                    holdings = holdings,
                    // Mock performance chart for now (last 7 days)
                    performance = Enumerable.Range(0, 7).Select(i => new {
                        date = DateTime.UtcNow.AddDays(-6 + i).ToString("MMM dd"),
                        value = totalValue * (1 + (decimal)(new Random().NextDouble() * 0.05 - 0.025))
                    })
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}

