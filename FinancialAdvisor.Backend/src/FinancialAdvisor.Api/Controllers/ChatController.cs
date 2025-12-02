using Microsoft.AspNetCore.Mvc;
using FinancialAdvisor.Application.Interfaces;
using FinancialAdvisor.Application.Models;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.Text;
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

        [HttpPost("stream")]
        public async Task StreamQuery([FromBody] ChatQueryRequest request)
        {
             if (string.IsNullOrWhiteSpace(request.Message))
            {
                Response.StatusCode = 400;
                await Response.WriteAsync("Message cannot be empty");
                return;
            }

            var sessionId = request.SessionId ?? "default_session";

            Response.Headers.Append("Content-Type", "text/plain; charset=utf-8");
            Response.Headers.Append("Cache-Control", "no-cache");
            Response.Headers.Append("Connection", "keep-alive");

            try
            {
                await foreach (var chunk in _ragService.ProcessQueryStreamAsync(request.Message, sessionId))
                {
                    var bytes = Encoding.UTF8.GetBytes(chunk);
                    await Response.Body.WriteAsync(bytes.AsMemory());
                    await Response.Body.FlushAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stream query failed");
                // In a stream, we can't change the status code once headers are sent, 
                // but we can try to send an error message if nothing was sent yet,
                // or just log it.
            }
        }
    }

    public class ChatQueryRequest
    {
        public string Message { get; set; }
        public string SessionId { get; set; }
    }
}
