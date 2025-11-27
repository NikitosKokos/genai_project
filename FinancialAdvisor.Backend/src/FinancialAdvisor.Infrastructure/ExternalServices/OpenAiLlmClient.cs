namespace FinancialAdvisor.Infrastructure.ExternalServices;

public class OpenAiLlmClient
{
    public Task<string> GenerateResponseAsync(string prompt)
    {
        return Task.FromResult("Response");
    }
}

