using FinancialAdvisor.Application.Interfaces;
using System.Threading.Tasks;

namespace FinancialAdvisor.Application.Interfaces
{
    public interface ILLMService
    {
        Task<string> GenerateFinancialAdviceAsync(string userQuery, string context, string sessionId);
    }
}

