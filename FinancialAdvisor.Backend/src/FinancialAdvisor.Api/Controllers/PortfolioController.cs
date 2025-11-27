using Microsoft.AspNetCore.Mvc;

namespace FinancialAdvisor.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PortfolioController : ControllerBase
{
    [HttpGet]
    public IActionResult GetPortfolio()
    {
        return Ok(new { message = "Portfolio endpoint" });
    }
}

