using FinancialAdvisor.Application.Models;
using System.Collections.Generic;

namespace FinancialAdvisor.Application.Interfaces
{
    public interface IPromptService
    {
        string ConstructSystemPrompt();
        string ConstructAugmentedUserPrompt(
            string userQuery,
            string portfolioContext,
            string marketContext,
            string ragContext,
            Session session
        );
        string PostProcessModelOutput(string modelOutput);
    }
}
