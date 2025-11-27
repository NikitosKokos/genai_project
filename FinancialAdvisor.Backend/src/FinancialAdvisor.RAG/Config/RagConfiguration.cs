namespace FinancialAdvisor.RAG.Config;

public class RagConfiguration
{
    public int TopK { get; set; } = 5;
    public int MaxTokens { get; set; } = 512;
}

