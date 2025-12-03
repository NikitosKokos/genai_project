using FinancialAdvisor.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
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
        private readonly string _ollamaEndpoint;
        private readonly string _modelName;
        private readonly ILogger<LLMService> _logger;

        public LLMService(HttpClient httpClient, IConfiguration configuration, ILogger<LLMService> logger)
        {
            _httpClient = httpClient;
            _ollamaEndpoint = configuration["OLLAMA_ENDPOINT"] ?? "http://ollama:11434";
            _modelName = configuration["OLLAMA_MODEL"] ?? "deepseek-r1:1.5b";
            _logger = logger;
        }

        public async Task<string> GenerateFinancialAdviceAsync(string userQuery, string context, string sessionId)
        {
            try
            {
                // Ollama API structure
                var prompt = $"{context}\n\nUser Query: {userQuery}\n\nResponse:";
                
                var request = new
                {
                    model = _modelName,
                    prompt = prompt,
                    stream = false,
                    keep_alive = -1,
                    think = false, // Disable native thinking output
                    options = new 
                    {
                        num_ctx = 4096,
                        num_gpu = 99
                    }
                };

                var response = await _httpClient.PostAsync($"{_ollamaEndpoint}/api/generate", new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json"));
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<OllamaResponse>();
                return result?.Response ?? "Sorry, I couldn't generate a response.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate LLM response");
                return "I apologize, but I'm currently unable to process your request due to a technical issue.";
            }
        }

        public async IAsyncEnumerable<string> GenerateFinancialAdviceStreamAsync(string userQuery, string context, string sessionId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var prompt = $"{context}\n\nUser Query: {userQuery}\n\nResponse:";
            
            var request = new
            {
                model = _modelName,
                prompt = prompt,
                stream = true,
                keep_alive = -1,
                think = false, // Disable native thinking output
                options = new 
                {
                    num_ctx = 4096,
                    num_gpu = 99
                }
            };

            var requestContent = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{_ollamaEndpoint}/api/generate") { Content = requestContent };
            
            using var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            string? line;
            var accumulatedText = "";

            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (cancellationToken.IsCancellationRequested) break;
                if (string.IsNullOrEmpty(line)) continue;

                OllamaResponse? chunk = null;
                try 
                {
                    chunk = JsonSerializer.Deserialize<OllamaResponse>(line);
                }
                catch (Exception ex)
                {
                     _logger.LogWarning(ex, "Failed to parse streaming chunk");
                }

                if (chunk != null)
                {
                    // IGNORE chunk.Thinking entirely for immediate response

                    // Handle Response field
                    if (!string.IsNullOrEmpty(chunk.Response))
                    {
                        var text = chunk.Response;

                        // Manual filter for raw thought tags if they leak in the text
                        if (text.Contains("<thought_process>") || text.Contains("</thought_process>") || text.Contains("<think>") || text.Contains("</think>"))
                        {
                            text = text.Replace("<thought_process>", "")
                                       .Replace("</thought_process>", "")
                                       .Replace("<think>", "")
                                       .Replace("</think>", "");
                        }
                        
                        // Hybrid logic to handle both Delta and Accumulated streaming from Ollama
                        if (accumulatedText.Length > 0 && text.StartsWith(accumulatedText))
                        {
                            // It's an accumulated chunk
                            var delta = text.Substring(accumulatedText.Length);
                            accumulatedText = text;
                            yield return delta;
                        }
                        else
                        {
                            // It's a standard delta chunk
                            accumulatedText += text;
                            yield return text;
                        }
                    }
                }
            }
        }

        private class OllamaResponse
        {
            [JsonPropertyName("response")]
            public string Response { get; set; }

            [JsonPropertyName("thinking")]
            public string Thinking { get; set; }
            
            [JsonPropertyName("done")]
            public bool Done { get; set; }
        }
    }
}