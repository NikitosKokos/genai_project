namespace FinancialAdvisor.Infrastructure.Repositories;

public class GenericRepository<T>
{
    public Task<T?> GetByIdAsync(int id)
    {
        return Task.FromResult<T?>(default);
    }
}

