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
    [Route("api/chat")]
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
            // Validate request
            if (request == null)
            {
                Response.StatusCode = 400;
                await Response.WriteAsync("Request body is required", HttpContext.RequestAborted);
                return;
            }

            if (string.IsNullOrWhiteSpace(request.Message))
            {
                Response.StatusCode = 400;
                await Response.WriteAsync("Message cannot be empty", HttpContext.RequestAborted);
                return;
            }

            Response.Headers.Append("Content-Type", "text/plain");
            Response.Headers.Append("Cache-Control", "no-cache");
            Response.Headers.Append("Connection", "keep-alive");

            var responseStream = Response.BodyWriter;
            var cancellation = HttpContext.RequestAborted;
            
            // Default to 3 if not specified or invalid
            int docCount = request.DocumentCount > 0 ? request.DocumentCount : 3;
            var sessionId = request.SessionId ?? "default_session";
            
            _logger.LogInformation($"[ChatController] StreamQuery received - SessionId: '{sessionId}', Message: '{request.Message?.Substring(0, Math.Min(50, request.Message?.Length ?? 0))}'");

            try
            {
                await foreach (var chunk in _ragService.ProcessQueryStreamAsync(request.Message, sessionId, cancellation, request.EnableReasoning, docCount))
                {
                    if (cancellation.IsCancellationRequested) break;

                    if (string.IsNullOrWhiteSpace(chunk))
                        continue;

                    try
                    {
                        var buffer = Encoding.UTF8.GetBytes(chunk);
                        await responseStream.WriteAsync(buffer, cancellation);
                        await responseStream.FlushAsync(cancellation);
                    }
                    catch (OperationCanceledException)
                    {
                        // Client disconnected - this is expected, just break
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error writing chunk to stream");
                        // Try to send error message to client
                        try
                        {
                            var errorMsg = Encoding.UTF8.GetBytes("<status>Error streaming response</status>");
                            await responseStream.WriteAsync(errorMsg, cancellation);
                            await responseStream.FlushAsync(cancellation);
                        }
                        catch
                        {
                            // If we can't send error, connection is likely dead
                        }
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Client disconnected - this is normal, just log and return
                _logger.LogInformation("Stream cancelled by client");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing stream query: {Message}", request.Message);
                
                // Try to send error message to client before closing
                try
                {
                    var errorMsg = Encoding.UTF8.GetBytes($"<status>Error: {ex.Message}</status>");
                    await responseStream.WriteAsync(errorMsg, cancellation);
                    await responseStream.FlushAsync(cancellation);
                }
                catch
                {
                    // Connection is already dead, can't send error
                }
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
