using FinancialAdvisor.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
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

                var response = await _httpClient.PostAsJsonAsync($"{_ollamaEndpoint}/api/generate", request);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<OllamaResponse>();
                return result?.Response ?? "Sorry, I couldn't generate a response.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate LLM response");
                // Return a fallback message or rethrow
                return "I apologize, but I'm currently unable to process your request due to a technical issue.";
            }
        }

        private class OllamaResponse
        {
            public string Response { get; set; }
        }
    }
}

