using FinancialAdvisor.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System;

namespace FinancialAdvisor.Infrastructure.ExternalServices
{
    public class EmbeddingService : IEmbeddingService
    {
        private readonly HttpClient _httpClient;
        private readonly string _serviceUrl;
        private readonly ILogger<EmbeddingService> _logger;

        public EmbeddingService(HttpClient httpClient, IConfiguration configuration, ILogger<EmbeddingService> logger)
        {
            _httpClient = httpClient;
            _serviceUrl = configuration["EMBEDDING_SERVICE_URL"] ?? "http://localhost:5001";
            _logger = logger;
        }

        public async Task<float[]> EmbedAsync(string text)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"{_serviceUrl}/embed", new { text });
                response.EnsureSuccessStatusCode();
                
                var result = await response.Content.ReadFromJsonAsync<EmbeddingResponse>();
                // Convert double[] to float[] if necessary, or use double[] in model.
                // Model uses double[]. Interface uses float[]. 
                // Let's update Interface and implementation to match Model (double[]) or convert.
                // User guide had float[]. Model has double[] (I changed it to be safe, but guide said float[]).
                // Let's stick to float[] for interface and convert.
                
                if (result?.Embedding == null) return Array.Empty<float>();

                var floats = new float[result.Embedding.Length];
                for (int i = 0; i < result.Embedding.Length; i++)
                {
                    floats[i] = (float)result.Embedding[i];
                }
                return floats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate embedding");
                throw;
            }
        }

        private class EmbeddingResponse
        {
            public double[] Embedding { get; set; }
        }
    }
}

