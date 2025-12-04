using System.Threading.Tasks;

namespace FinancialAdvisor.Application.Interfaces
{
    public interface ITool
    {
        string Name { get; }
        string Description { get; }
        // Returns JSON string
        Task<string> ExecuteAsync(string argsJson);
    }
}

