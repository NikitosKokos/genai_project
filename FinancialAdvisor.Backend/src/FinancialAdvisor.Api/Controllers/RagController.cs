using Microsoft.AspNetCore.Mvc;
using FinancialAdvisor.Application.Interfaces;
using FinancialAdvisor.Application.DTOs;

namespace FinancialAdvisor.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RagController : ControllerBase
{
    private readonly IRagService _ragService;

    public RagController(IRagService ragService)
    {
        _ragService = ragService;
    }

    [HttpPost("query")]
    public async Task<IActionResult> Query([FromBody] RagQueryDto queryDto)
    {
        var result = await _ragService.QueryAsync(queryDto.Query);
        return Ok(result);
    }
}

