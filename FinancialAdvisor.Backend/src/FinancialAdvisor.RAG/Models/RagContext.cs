namespace FinancialAdvisor.RAG.Models;

public class RagContext
{
    public string Query { get; set; } = string.Empty;
    public object[] RetrievedDocuments { get; set; } = Array.Empty<object>();
}

