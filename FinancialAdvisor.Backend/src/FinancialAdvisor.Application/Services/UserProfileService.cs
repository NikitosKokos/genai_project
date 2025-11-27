namespace FinancialAdvisor.Application.Services;

public class UserProfileService
{
    public Task<object> GetProfileAsync(int userId)
    {
        return Task.FromResult<object>(new { userId });
    }
}

