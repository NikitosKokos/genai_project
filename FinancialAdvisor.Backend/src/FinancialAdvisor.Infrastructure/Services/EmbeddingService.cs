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
    public class EmbeddingService : IEmbeddingService
    {
        private readonly HttpClient _httpClient;
        private readonly string _embeddingServiceUrl;
        private readonly ILogger<EmbeddingService> _logger;

        public EmbeddingService(HttpClient httpClient, IConfiguration configuration, ILogger<EmbeddingService> logger)
        {
            _httpClient = httpClient;
            _embeddingServiceUrl = configuration["EMBEDDING_SERVICE_URL"] ?? "http://embedding-service:5001";
            _logger = logger;
        }

        public async Task<float[]> EmbedAsync(string text)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"{_embeddingServiceUrl}/embed", new { text });
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<EmbeddingResponse>();
                return result?.Embedding ?? Array.Empty<float>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate embedding");
                throw;
            }
        }

        private class EmbeddingResponse
        {
            public float[] Embedding { get; set; }
        }
    }
}

