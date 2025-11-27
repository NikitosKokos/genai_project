namespace FinancialAdvisor.Infrastructure.Repositories;

public class TransactionRepository
{
    public Task<object> GetByUserIdAsync(int userId)
    {
        return Task.FromResult<object>(new { userId });
    }
}

