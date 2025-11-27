namespace FinancialAdvisor.Application.Services;

public class DummyDataGeneratorService
{
    public Task<object> GenerateDummyDataAsync()
    {
        return Task.FromResult<object>(new { message = "Dummy data" });
    }
}

