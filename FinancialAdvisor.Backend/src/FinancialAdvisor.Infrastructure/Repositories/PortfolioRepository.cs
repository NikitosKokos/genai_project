namespace FinancialAdvisor.Infrastructure.Repositories;

public class PortfolioRepository
{
    public Task<object> GetByUserIdAsync(int userId)
    {
        return Task.FromResult<object>(new { userId });
    }
}

