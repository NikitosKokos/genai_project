using Microsoft.AspNetCore.Mvc;

namespace FinancialAdvisor.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TransactionController : ControllerBase
{
    [HttpGet]
    public IActionResult GetTransactions()
    {
        return Ok(new { message = "Transaction endpoint" });
    }
}

