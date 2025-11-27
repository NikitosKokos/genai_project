using Microsoft.AspNetCore.Mvc;

namespace FinancialAdvisor.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MarketDataController : ControllerBase
{
    [HttpGet("{symbol}")]
    public IActionResult GetMarketData(string symbol)
    {
        return Ok(new { message = $"Market data for {symbol}" });
    }
}

