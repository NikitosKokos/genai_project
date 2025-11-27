using Microsoft.AspNetCore.Mvc;

namespace FinancialAdvisor.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProfileController : ControllerBase
{
    [HttpGet]
    public IActionResult GetProfile()
    {
        return Ok(new { message = "Profile endpoint" });
    }
}

