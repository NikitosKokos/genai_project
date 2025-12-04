namespace FinancialAdvisor.RAG.Models;

public class RetrievalResult
{
    public EmbeddingedDocument Document { get; set; } = new();
    public float Score { get; set; }
}

