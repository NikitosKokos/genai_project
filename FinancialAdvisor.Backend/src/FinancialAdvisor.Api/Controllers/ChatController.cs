using Microsoft.AspNetCore.Mvc;
using FinancialAdvisor.Application.Interfaces;
using FinancialAdvisor.Application.Models;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using Microsoft.Extensions.Logging;

namespace FinancialAdvisor.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly IRagService _ragService;
        private readonly ILogger<ChatController> _logger;

        public ChatController(IRagService ragService, ILogger<ChatController> logger)
        {
            _ragService = ragService;
            _logger = logger;
        }

        [HttpPost("query")]
        public async Task<IActionResult> SubmitQuery([FromBody] ChatQueryRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
                return BadRequest("Message cannot be empty");

            var sessionId = request.SessionId ?? "default_session";

            try
            {
                var response = await _ragService.ProcessQueryAsync(request.Message, sessionId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chat query failed");
                return StatusCode(500, "Internal server error");
            }
        }
    }

    public class ChatQueryRequest
    {
        public string Message { get; set; }
        public string SessionId { get; set; }
    }
}

