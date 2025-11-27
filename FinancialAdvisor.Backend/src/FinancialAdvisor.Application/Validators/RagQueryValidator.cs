namespace FinancialAdvisor.Application.Validators;

public class RagQueryValidator
{
    public bool Validate(string query)
    {
        return !string.IsNullOrWhiteSpace(query);
    }
}

