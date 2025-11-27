namespace FinancialAdvisor.Application.Interfaces;

public interface IUserProfileService
{
    Task<object> GetProfileAsync(int userId);
}

