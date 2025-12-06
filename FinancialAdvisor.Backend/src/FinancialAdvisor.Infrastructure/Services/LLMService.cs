using FinancialAdvisor.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace FinancialAdvisor.Infrastructure.Services
{
    public class LLMService : ILLMService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiEndpoint;
        private readonly string _apiKey;
        private readonly string _modelName;
        private readonly string _reasoningModel;
        private readonly ILogger<LLMService> _logger;
        private readonly bool _useDeepSeek;

        public LLMService(HttpClient httpClient, IConfiguration configuration, ILogger<LLMService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            
            // Check if DeepSeek API key is configured
            _apiKey = configuration["DEEPSEEK_API_KEY"] ?? "";
            _useDeepSeek = !string.IsNullOrEmpty(_apiKey);
            
            if (_useDeepSeek)
            {
                _apiEndpoint = configuration["DEEPSEEK_ENDPOINT"] ?? "https://api.deepseek.com";
                _modelName = configuration["DEEPSEEK_MODEL"] ?? "deepseek-chat";
                _reasoningModel = configuration["DEEPSEEK_REASONING_MODEL"] ?? "deepseek-reasoner";
                _logger.LogInformation("Using DeepSeek API with model: {Model}", _modelName);
            }
            else
            {
                // Fallback to Ollama
                _apiEndpoint = configuration["OLLAMA_ENDPOINT"] ?? "http://ollama:11434";
                _modelName = configuration["OLLAMA_MODEL"] ?? "llama3.1:8b";
                _reasoningModel = _modelName;
                _logger.LogInformation("Using Ollama with model: {Model}", _modelName);
            }
        }

        public async Task<string> GenerateFinancialAdviceAsync(string userQuery, string context, string sessionId)
        {
            try
            {
                if (_useDeepSeek)
                {
                    return await GenerateWithDeepSeekAsync(userQuery, context, false);
                }
                else
                {
                    return await GenerateWithOllamaAsync(userQuery, context);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate LLM response");
                return "I apologize, but I'm currently unable to process your request due to a technical issue.";
            }
        }

        public async IAsyncEnumerable<string> GenerateFinancialAdviceStreamAsync(
            string userQuery, 
            string context, 
            string sessionId, 
            [EnumeratorCancellation] CancellationToken cancellationToken = default, 
            bool enableReasoning = false)
        {
            if (_useDeepSeek)
            {
                await foreach (var chunk in StreamWithDeepSeekAsync(userQuery, context, enableReasoning, cancellationToken))
                {
                    yield return chunk;
                }
            }
            else
            {
                await foreach (var chunk in StreamWithOllamaAsync(userQuery, context, enableReasoning, cancellationToken))
                {
                    yield return chunk;
                }
            }
        }

        #region DeepSeek Implementation

        private async Task<string> GenerateWithDeepSeekAsync(string userQuery, string context, bool enableReasoning)
        {
            var model = enableReasoning ? _reasoningModel : _modelName;
            
            var request = new
            {
                model = model,
                messages = new[]
                {
                    new { role = "system", content = context },
                    new { role = "user", content = userQuery }
                },
                stream = false,
                max_tokens = 2048,
                temperature = 0.7
            };

            var requestContent = new StringContent(
                JsonSerializer.Serialize(request), 
                Encoding.UTF8, 
                "application/json");

            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{_apiEndpoint}/v1/chat/completions")
            {
                Content = requestContent
            };
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            var response = await _httpClient.SendAsync(requestMessage);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<DeepSeekResponse>(responseJson);
            
            return result?.Choices?[0]?.Message?.Content ?? "Sorry, I couldn't generate a response.";
        }

        private async IAsyncEnumerable<string> StreamWithDeepSeekAsync(
            string userQuery, 
            string context, 
            bool enableReasoning,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var model = enableReasoning ? _reasoningModel : _modelName;
            _logger.LogInformation("DeepSeek streaming with model: {Model}, reasoning: {Reasoning}", model, enableReasoning);

            var request = new
            {
                model = model,
                messages = new[]
                {
                    new { role = "system", content = context },
                    new { role = "user", content = userQuery }
                },
                stream = true,
                max_tokens = 2048,
                temperature = 0.7
            };

            var requestContent = new StringContent(
                JsonSerializer.Serialize(request), 
                Encoding.UTF8, 
                "application/json");

            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{_apiEndpoint}/v1/chat/completions")
            {
                Content = requestContent
            };
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            using var response = await _httpClient.SendAsync(
                requestMessage, 
                HttpCompletionOption.ResponseHeadersRead, 
                cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("DeepSeek API error: {StatusCode} - {Error}", response.StatusCode, errorContent);
                yield return $"<status>Error: DeepSeek API returned {response.StatusCode}</status>";
                yield break;
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            var thinkingBuffer = new StringBuilder();
            string? errorMessage = null;
            bool wasCancelled = false;
            
            // Read stream with error handling
            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                // Use helper method to safely read line (avoids yield in try-catch restriction)
                var readResult = await ReadLineSafelyAsync(reader, cancellationToken);
                if (readResult.IsCancelled)
                {
                    wasCancelled = true;
                    _logger.LogInformation("DeepSeek stream cancelled");
                    break;
                }
                if (readResult.HasError)
                {
                    errorMessage = readResult.ErrorMessage;
                    break;
                }
                
                var line = readResult.Line;
                if (string.IsNullOrEmpty(line)) continue;
                
                // SSE format: "data: {...}"
                if (!line.StartsWith("data: ")) continue;
                
                var jsonData = line.Substring(6); // Remove "data: " prefix
                if (jsonData == "[DONE]") break;

                DeepSeekStreamChunk? chunk = null;
                try
                {
                    chunk = JsonSerializer.Deserialize<DeepSeekStreamChunk>(jsonData);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse DeepSeek stream chunk: {Data}", jsonData);
                    continue;
                }

                if (chunk?.Choices == null || chunk.Choices.Length == 0) continue;

                var delta = chunk.Choices[0].Delta;
                
                // Handle reasoning_content (DeepSeek R1's thinking)
                if (!string.IsNullOrEmpty(delta?.ReasoningContent))
                {
                    thinkingBuffer.Append(delta.ReasoningContent);
                    
                    // Emit thinking in batches for better UX
                    if (thinkingBuffer.Length > 50 || 
                        delta.ReasoningContent.EndsWith("\n") ||
                        delta.ReasoningContent.EndsWith(". "))
                    {
                        var escapedThinking = thinkingBuffer.ToString()
                            .Replace("&", "&amp;")
                            .Replace("<", "&lt;")
                            .Replace(">", "&gt;");
                        yield return $"<thinking>{escapedThinking}</thinking>";
                        thinkingBuffer.Clear();
                    }
                }

                // Handle regular content
                if (!string.IsNullOrEmpty(delta?.Content))
                {
                    var content = delta.Content;
                    
                    // Filter out any leaked thinking tags
                    content = content
                        .Replace("<think>", "")
                        .Replace("</think>", "")
                        .Replace("<thought>", "")
                        .Replace("</thought>", "");
                    
                    if (!string.IsNullOrEmpty(content))
                    {
                        var safeContent = content.Replace("]]>", "]]]]><![CDATA[>");
                        yield return $"<response><![CDATA[{safeContent}]]></response>";
                    }
                }
            }

            // Emit any remaining thinking buffer (outside try-catch)
            if (thinkingBuffer.Length > 0)
            {
                var escapedThinking = thinkingBuffer.ToString()
                    .Replace("&", "&amp;")
                    .Replace("<", "&lt;")
                    .Replace(">", "&gt;");
                yield return $"<thinking>{escapedThinking}</thinking>";
            }

            // Handle errors and cancellation (outside try-catch)
            if (wasCancelled)
            {
                yield return "<status>Stream cancelled</status>";
            }
            else if (!string.IsNullOrEmpty(errorMessage))
            {
                yield return $"<status>Error: {errorMessage}</status>";
            }
        }

        #endregion

        #region Ollama Implementation (Fallback)

        private async Task<string> GenerateWithOllamaAsync(string userQuery, string context)
        {
            var prompt = $"{context}\n\nUser Query: {userQuery}\n\nResponse:";
            
            var request = new
            {
                model = _modelName,
                prompt = prompt,
                stream = false,
                keep_alive = -1,
                options = new { num_ctx = 4096 }
            };

            var response = await _httpClient.PostAsync(
                $"{_apiEndpoint}/api/generate", 
                new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json"));
            
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OllamaResponse>();
            return result?.Response ?? "Sorry, I couldn't generate a response.";
        }

        private async IAsyncEnumerable<string> StreamWithOllamaAsync(
            string userQuery, 
            string context, 
            bool enableReasoning,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var prompt = $"{context}\n\nUser Query: {userQuery}\n\nResponse:";
            
            var request = new
            {
                model = _modelName,
                prompt = prompt,
                stream = true,
                keep_alive = -1,
                think = enableReasoning,
                options = new { num_ctx = 4096 }
            };

            var requestContent = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{_apiEndpoint}/api/generate") 
            { 
                Content = requestContent 
            };
            
            HttpResponseMessage? response = null;
            string? connectionError = null;
            
            try
            {
                response = await _httpClient.SendAsync(
                    requestMessage, 
                    HttpCompletionOption.ResponseHeadersRead, 
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to Ollama at {Endpoint}", _apiEndpoint);
                connectionError = $"Failed to connect to Ollama. Please check if Ollama is running at {_apiEndpoint}";
            }
            
            if (connectionError != null)
            {
                yield return $"<status>Error: {connectionError}</status>";
                yield break;
            }
            
            if (response == null)
            {
                yield return "<status>Error: No response from Ollama</status>";
                yield break;
            }
            
            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("Ollama API error: {StatusCode} - {Error}", response.StatusCode, errorContent);
                    
                    // Provide more helpful error messages based on status code
                    string httpErrorMessage;
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        httpErrorMessage = $"Model '{_modelName}' not found in Ollama. Please pull the model first by running: docker exec fin-advisor-ollama ollama pull {_modelName}";
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
                    {
                        httpErrorMessage = "Ollama service is unavailable. Please check if Ollama is running and accessible.";
                    }
                    else
                    {
                        httpErrorMessage = $"Ollama API returned {response.StatusCode}. Error: {errorContent}";
                    }
                    
                    yield return $"<status>Error: {httpErrorMessage}</status>";
                    yield break;
                }

                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var reader = new StreamReader(stream);

                var accumulatedThinking = new StringBuilder();
                string? errorMessage = null;
                bool wasCancelled = false;

                // Read stream with error handling
                while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
                {
                    // Use helper method to safely read line (avoids yield in try-catch restriction)
                    var readResult = await ReadLineSafelyAsync(reader, cancellationToken);
                    if (readResult.IsCancelled)
                    {
                        wasCancelled = true;
                        _logger.LogInformation("Ollama stream cancelled");
                        break;
                    }
                    if (readResult.HasError)
                    {
                        // Enhance error message for Ollama-specific context
                        var baseError = readResult.ErrorMessage ?? "Stream read failed";
                        errorMessage = baseError.Contains("terminated", StringComparison.OrdinalIgnoreCase) 
                            ? "Connection to Ollama was terminated. Please check if Ollama is running."
                            : baseError;
                        break;
                    }
                    
                    var line = readResult.Line;
                    if (string.IsNullOrEmpty(line)) continue;

                    OllamaResponse? chunk = null;
                    try 
                    {
                        chunk = JsonSerializer.Deserialize<OllamaResponse>(line);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse Ollama chunk: {Line}", line);
                        continue;
                    }

                    if (chunk == null) continue;

                    // Check if done
                    if (chunk.Done) break;

                    // Handle thinking
                    if (enableReasoning && !string.IsNullOrEmpty(chunk.Thinking))
                    {
                        accumulatedThinking.Append(chunk.Thinking);
                        
                        if (accumulatedThinking.Length > 100 || 
                            chunk.Thinking.EndsWith("\n") || 
                            chunk.Thinking.EndsWith(". "))
                        {
                            var escapedThinking = accumulatedThinking.ToString()
                                .Replace("&", "&amp;")
                                .Replace("<", "&lt;")
                                .Replace(">", "&gt;");
                            yield return $"<thinking>{escapedThinking}</thinking>";
                            accumulatedThinking.Clear();
                        }
                    }

                    // Handle response
                    if (!string.IsNullOrEmpty(chunk.Response))
                    {
                        var text = chunk.Response
                            .Replace("<think>", "")
                            .Replace("</think>", "");
                        
                        if (!string.IsNullOrEmpty(text))
                        {
                            var safeText = text.Replace("]]>", "]]]]><![CDATA[>");
                            yield return $"<response><![CDATA[{safeText}]]></response>";
                        }
                    }
                }

                // Emit remaining thinking (outside try-catch)
                if (accumulatedThinking.Length > 0)
                {
                    var escapedThinking = accumulatedThinking.ToString()
                        .Replace("&", "&amp;")
                        .Replace("<", "&lt;")
                        .Replace(">", "&gt;");
                    yield return $"<thinking>{escapedThinking}</thinking>";
                }

                // Handle errors and cancellation (outside try-catch)
                if (wasCancelled)
                {
                    yield return "<status>Stream cancelled</status>";
                }
                else if (!string.IsNullOrEmpty(errorMessage))
                {
                    // Provide more helpful error messages
                    var finalErrorMsg = errorMessage;
                    if (errorMessage.Contains("terminated", StringComparison.OrdinalIgnoreCase))
                    {
                        finalErrorMsg = "Connection to Ollama was terminated. Please check if Ollama is running and accessible.";
                    }
                    else if (errorMessage.Contains("connection", StringComparison.OrdinalIgnoreCase))
                    {
                        finalErrorMsg = $"Connection error: {errorMessage}. Please check if Ollama is running at {_apiEndpoint}";
                    }
                    
                    yield return $"<status>Error: {finalErrorMsg}</status>";
                }
            }
        }

        #endregion

        #region Helper Methods

        private async Task<ReadLineResult> ReadLineSafelyAsync(StreamReader reader, CancellationToken cancellationToken)
        {
            try
            {
                var line = await reader.ReadLineAsync();
                return new ReadLineResult { Line = line };
            }
            catch (OperationCanceledException)
            {
                return new ReadLineResult { IsCancelled = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading from stream: {ExceptionType} - {Message}", ex.GetType().Name, ex.Message);
                
                var errorMsg = ex.Message.Contains("terminated", StringComparison.OrdinalIgnoreCase) 
                    ? "Connection was terminated"
                    : $"Stream read failed: {ex.Message}";
                
                return new ReadLineResult { HasError = true, ErrorMessage = errorMsg };
            }
        }

        private class ReadLineResult
        {
            public string? Line { get; set; }
            public bool IsCancelled { get; set; }
            public bool HasError { get; set; }
            public string? ErrorMessage { get; set; }
        }

        #endregion

        #region Response Models

        private class DeepSeekResponse
        {
            [JsonPropertyName("choices")]
            public DeepSeekChoice[]? Choices { get; set; }
        }

        private class DeepSeekChoice
        {
            [JsonPropertyName("message")]
            public DeepSeekMessage? Message { get; set; }
        }

        private class DeepSeekMessage
        {
            [JsonPropertyName("content")]
            public string? Content { get; set; }
            
            [JsonPropertyName("reasoning_content")]
            public string? ReasoningContent { get; set; }
        }

        private class DeepSeekStreamChunk
        {
            [JsonPropertyName("choices")]
            public DeepSeekStreamChoice[]? Choices { get; set; }
        }

        private class DeepSeekStreamChoice
        {
            [JsonPropertyName("delta")]
            public DeepSeekDelta? Delta { get; set; }
        }

        private class DeepSeekDelta
        {
            [JsonPropertyName("content")]
            public string? Content { get; set; }
            
            [JsonPropertyName("reasoning_content")]
            public string? ReasoningContent { get; set; }
        }

        private class OllamaResponse
        {
            [JsonPropertyName("response")]
            public string? Response { get; set; }

            [JsonPropertyName("thinking")]
            public string? Thinking { get; set; }
            
            [JsonPropertyName("done")]
            public bool Done { get; set; }
        }

        #endregion
    }
}
