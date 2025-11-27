namespace FinancialAdvisor.Infrastructure.Repositories;

public class UserProfileRepository
{
    public Task<object> GetByIdAsync(int id)
    {
        return Task.FromResult<object>(new { id });
    }
}

