namespace FinancialAdvisor.RAG.Services;

public class DocumentChunker
{
    public string[] ChunkDocument(string document, int maxTokens = 512)
    {
        return new[] { document };
    }
}

