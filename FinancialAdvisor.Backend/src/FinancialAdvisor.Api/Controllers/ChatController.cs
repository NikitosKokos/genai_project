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
            Response.Headers.Add("Content-Type", "text/plain");
            Response.Headers.Add("Cache-Control", "no-cache");
            Response.Headers.Add("Connection", "keep-alive");

            var responseStream = Response.BodyWriter;
            var cancellation = HttpContext.RequestAborted;
            
            // Default to 3 if not specified or invalid
            int docCount = request.DocumentCount > 0 ? request.DocumentCount : 3;

            await foreach (var chunk in _ragService.ProcessQueryStreamAsync(request.Message, request.SessionId, cancellation, request.EnableReasoning, docCount))
            {
                if (cancellation.IsCancellationRequested) break;

                if (string.IsNullOrWhiteSpace(chunk))
                    continue;

                var buffer = Encoding.UTF8.GetBytes(chunk);

                await responseStream.WriteAsync(buffer, cancellation);
                await responseStream.FlushAsync(cancellation);
            }
        }
    }

    public class ChatQueryRequest
    {
        public string Message { get; set; }
        public string SessionId { get; set; }
        public bool EnableReasoning { get; set; } = false;
        public int DocumentCount { get; set; } = 3;
    }
}
