using FinancialAdvisor.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System;
using System.Text.Json;
using System.Text;

namespace FinancialAdvisor.Infrastructure.ExternalServices
{
    public class LLMService : ILLMService
    {
        private readonly HttpClient _httpClient;
        private readonly string _ollamaEndpoint;
        private readonly ILogger<LLMService> _logger;

        public LLMService(HttpClient httpClient, IConfiguration configuration, ILogger<LLMService> logger)
        {
            _httpClient = httpClient;
            _ollamaEndpoint = configuration["OLLAMA_ENDPOINT"] ?? "http://localhost:11434";
            _logger = logger;
        }

        public async Task<string> GenerateFinancialAdviceAsync(string userQuery, string context, string sessionId)
        {
            try
            {
                var prompt = $"{context}\n\nUser Query: {userQuery}";
                
                var request = new
                {
                    model = "mistral", // or any available model
                    prompt = prompt,
                    stream = false
                };

                var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{_ollamaEndpoint}/api/generate", content);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<OllamaResponse>();
                return result?.Response ?? "Sorry, I could not generate advice.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate LLM response");
                return "Error generating advice.";
            }
        }

        private class OllamaResponse
        {
            public string Response { get; set; }
        }
    }
}

