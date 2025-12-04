using FinancialAdvisor.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FinancialAdvisor.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MarketController : ControllerBase
    {
        private readonly IMarketDataService _marketDataService;

        public MarketController(IMarketDataService marketDataService)
        {
            _marketDataService = marketDataService;
        }

        [HttpGet("{symbol}")]
        public async Task<IActionResult> GetMarketData(string symbol)
        {
            var data = await _marketDataService.GetMarketDataAsync(new List<string> { symbol });
            return Ok(data);
        }
        
        [HttpPost("batch")]
        public async Task<IActionResult> GetBatchMarketData([FromBody] List<string> symbols)
        {
            var data = await _marketDataService.GetMarketDataAsync(symbols);
            return Ok(data);
        }
    }
}
