using Microsoft.AspNetCore.Mvc;

namespace FinancialAdvisor.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RagController : ControllerBase
{
    [HttpPost("query")]
    public IActionResult Query([FromBody] object query)
    {
        return Ok(new { message = "RAG query endpoint" });
    }
}

