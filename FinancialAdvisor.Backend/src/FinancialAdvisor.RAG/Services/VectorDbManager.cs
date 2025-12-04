using FinancialAdvisor.RAG.Models;

namespace FinancialAdvisor.RAG.Services;

public class VectorDbManager
{
    private readonly List<EmbeddingedDocument> _documents = new();

    public Task UpsertAsync(EmbeddingedDocument doc)
    {
        _documents.Add(doc);
        return Task.CompletedTask;
    }

    public Task<RetrievalResult[]> SearchAsync(float[] queryVector, int topK = 3)
    {
        if (queryVector.Length == 0 || _documents.Count == 0)
            return Task.FromResult(Array.Empty<RetrievalResult>());

        var results = _documents
            .Select(doc => new RetrievalResult
            {
                Document = doc,
                Score = (float)CosineSimilarity(queryVector, doc.Embedding)
            })
            .OrderByDescending(r => r.Score)
            .Take(topK)
            .ToArray();

        return Task.FromResult(results);
    }

    private double CosineSimilarity(float[] v1, float[] v2)
    {
        if (v1.Length != v2.Length) return 0;

        double dot = 0.0, mag1 = 0.0, mag2 = 0.0;
        for (int i = 0; i < v1.Length; i++)
        {
            dot += v1[i] * v2[i];
            mag1 += v1[i] * v1[i];
            mag2 += v2[i] * v2[i];
        }

        return dot / (Math.Sqrt(mag1) * Math.Sqrt(mag2));
    }
}

