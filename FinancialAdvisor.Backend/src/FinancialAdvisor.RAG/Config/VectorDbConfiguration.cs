namespace FinancialAdvisor.RAG.Config;

public class VectorDbConfiguration
{
    public string Provider { get; set; } = "Pinecone";
    public string ApiKey { get; set; } = string.Empty;
}

