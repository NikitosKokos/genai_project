namespace FinancialAdvisor.Application.Services;

public class PortfolioService
{
    public Task<object> GetPortfolioAsync(int userId)
    {
        return Task.FromResult<object>(new { userId });
    }
}

