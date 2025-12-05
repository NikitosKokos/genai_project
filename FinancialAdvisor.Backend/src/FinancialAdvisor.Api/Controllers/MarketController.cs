using FinancialAdvisor.Application.Interfaces;
using FinancialAdvisor.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FinancialAdvisor.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MarketController : ControllerBase
    {
        private readonly IMarketDataService _marketDataService;
        private readonly MongoDbContext _mongoContext;

        public MarketController(IMarketDataService marketDataService, MongoDbContext mongoContext)
        {
            _marketDataService = marketDataService;
            _mongoContext = mongoContext;
        }

        [HttpGet("{symbol}")]
        public async Task<IActionResult> GetMarketData(string symbol)
        {
            var data = await _marketDataService.GetMarketDataAsync(new List<string> { symbol });
            if (data == null || !data.Any())
            {
                return NotFound(new { error = $"No market data found for symbol: {symbol}" });
            }
            return Ok(data);
        }
        
        [HttpPost("batch")]
        public async Task<IActionResult> GetBatchMarketData([FromBody] List<string> symbols)
        {
            var data = await _marketDataService.GetMarketDataAsync(symbols);
            return Ok(data);
        }
        
        [HttpPost("sync")]
        public async Task<IActionResult> ForceSyncMarketData()
        {
            // This endpoint can be called to manually trigger a sync
            // Useful for testing and debugging
            // Gets all active assets from the database (same as background service)
            try
            {
                var activeAssets = await _mongoContext.Assets
                    .Find(a => a.IsActive)
                    .ToListAsync();

                if (!activeAssets.Any())
                {
                    return Ok(new { 
                        message = "No active assets found in database",
                        symbolsRequested = 0,
                        symbolsFetched = 0,
                        data = new List<object>()
                    });
                }

                var symbols = activeAssets
                    .Where(a => !string.IsNullOrWhiteSpace(a.Symbol))
                    .Select(a => a.Symbol!)
                    .ToList();

                var data = await _marketDataService.GetMarketDataAsync(symbols);
                
                return Ok(new { 
                    message = "Market data sync completed",
                    symbolsRequested = symbols.Count,
                    symbolsFetched = data?.Count ?? 0,
                    symbols = symbols,
                    data = data
                });
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { 
                    error = "Failed to sync market data",
                    message = ex.Message
                });
            }
        }
    }
}
