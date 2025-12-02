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
using System.Threading.Tasks;

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
            _modelName = configuration["OLLAMA_MODEL"] ?? "mistral";
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
                    stream = false
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

        public async IAsyncEnumerable<string> GenerateFinancialAdviceStreamAsync(string userQuery, string context, string sessionId)
        {
            var prompt = $"{context}\n\nUser Query: {userQuery}\n\nResponse:";
            
            var request = new
            {
                model = _modelName,
                prompt = prompt,
                stream = true
            };

            var requestContent = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{_ollamaEndpoint}/api/generate") { Content = requestContent };
            
            using var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
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

                if (chunk != null && !string.IsNullOrEmpty(chunk.Response))
                {
                    yield return chunk.Response;
                }
            }
        }

        private class OllamaResponse
        {
            [JsonPropertyName("response")]
            public string Response { get; set; }
            
            [JsonPropertyName("done")]
            public bool Done { get; set; }
        }
    }
}
