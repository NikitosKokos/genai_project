namespace FinancialAdvisor.RAG.Services;

public class PromptEngineer
{
    public string BuildPrompt(string userQuery, object context)
    {
        return $"System prompt with context: {userQuery}";
    }
}

