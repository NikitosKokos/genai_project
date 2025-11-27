namespace FinancialAdvisor.RAG.Services;

public class VectorDbManager
{
    public Task UpsertAsync(string id, float[] vector, object metadata)
    {
        return Task.CompletedTask;
    }

    public Task<object[]> SearchAsync(float[] queryVector, int topK)
    {
        return Task.FromResult(new object[0]);
    }
}

