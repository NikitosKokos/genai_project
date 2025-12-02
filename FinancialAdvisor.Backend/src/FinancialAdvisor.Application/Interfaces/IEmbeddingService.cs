using System.Threading.Tasks;

namespace FinancialAdvisor.Application.Interfaces
{
    public interface IEmbeddingService
    {
        Task<float[]> EmbedAsync(string text);
    }
}

