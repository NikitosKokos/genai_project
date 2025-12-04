namespace FinancialAdvisor.RAG.Models;

public class EmbeddingedDocument
{
    public string Id { get; set; } = string.Empty;
    public float[] Embedding { get; set; } = Array.Empty<float>();
    public string Content { get; set; } = string.Empty;
    public string Metadata { get; set; } = string.Empty;
}

