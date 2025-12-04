using System;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using FinancialAdvisor.Application.Interfaces;
using FinancialAdvisor.Infrastructure.Data;
using FinancialAdvisor.Application.Models;
using MongoDB.Driver;

namespace FinancialAdvisor.Infrastructure.Tools
{
    public class SearchRagTool : ITool
    {
        private readonly IEmbeddingService _embeddingService;
        private readonly MongoDbContext _mongoContext;

        public SearchRagTool(IEmbeddingService embeddingService, MongoDbContext mongoContext)
        {
            _embeddingService = embeddingService;
            _mongoContext = mongoContext;
        }

        public string Name => "search_rag";
        public string Description => "Search relevant financial news and documents. Args: query (string), top_k (int)";

        public async Task<string> ExecuteAsync(string argsJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(argsJson);
                var query = doc.RootElement.GetProperty("query").GetString();
                int topK = 3;
                if (doc.RootElement.TryGetProperty("top_k", out var kProp))
                {
                    topK = kProp.GetInt32();
                }

                var embedding = await _embeddingService.EmbedAsync(query);
                
                // Perform Vector Search
                // Note: In a real prod environment, use Atlas Search or a proper Vector Store. 
                // Here we do in-memory cosine similarity for the MVP as seen in RagOrchestrator.
                 var allDocs = await _mongoContext.FinancialDocuments
                    .Find(Builders<FinancialDocument>.Filter.Empty)
                    .ToListAsync();

                var results = allDocs
                    .Select(d => new 
                    {
                        doc = d,
                        score = CosineSimilarity(embedding, d.Embedding)
                    })
                    .OrderByDescending(x => x.score)
                    .Take(topK)
                    .Select(x => new 
                    {
                        id = x.doc.Id.ToString(),
                        title = x.doc.Title,
                        snippet = x.doc.Content.Length > 200 ? x.doc.Content.Substring(0, 200) + "..." : x.doc.Content,
                        timestamp = x.doc.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                        source = x.doc.Source,
                        score = x.score
                    })
                    .ToList();

                return JsonSerializer.Serialize(results);
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { error = ex.Message });
            }
        }

        private double CosineSimilarity(float[] query, double[] docEmbedding)
        {
            if (query == null || docEmbedding == null) return 0;
            // Convert double[] to float[] for comparison if needed or just calc
            var length = Math.Min(query.Length, docEmbedding.Length);
            double dotProduct = 0, normA = 0, normB = 0;
            for (int i = 0; i < length; i++)
            {
                dotProduct += query[i] * docEmbedding[i];
                normA += query[i] * query[i];
                normB += docEmbedding[i] * docEmbedding[i];
            }
            if (normA == 0 || normB == 0) return 0;
            return dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
        }
    }
}

