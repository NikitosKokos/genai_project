namespace FinancialAdvisor.Application.Services;

public class PortfolioService
{
    public Task<object> GetPortfolioAsync(string sessionId)
    {
        return Task.FromResult<object>(new { sessionId });
    }
}

