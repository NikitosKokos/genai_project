namespace FinancialAdvisor.Infrastructure.Repositories;

public class UnitOfWork
{
    public Task<int> SaveChangesAsync()
    {
        return Task.FromResult(0);
    }
}

