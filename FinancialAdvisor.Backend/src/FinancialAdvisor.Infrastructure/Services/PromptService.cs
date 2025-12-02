using FinancialAdvisor.Application.Interfaces;
using FinancialAdvisor.Application.Models;

namespace FinancialAdvisor.Infrastructure.Services
{
    public class PromptService : IPromptService
    {
        public string ConstructSystemPrompt()
        {
            return @"You are the senior financial portfolio manager.
Your advice is the final word for the client. 
You are responsible for the investment strategy.

INSTRUCTIONS:
1. First, provide a DETAILED analysis of the market situation and the specific asset.
2. Explain your reasoning clearly, referencing the provided news and portfolio context.
3. Be decisive in your conclusion (BUY/SELL/HOLD).
4. NEVER use phrases like 'consult a financial advisor' or 'seek professional advice'. Take responsibility.
5. AFTER your analysis is complete, append the required JSON block.

The JSON block MUST be the very last thing in your response.

Your output must end with this JSON format:
```json
{
  ""trades"": [ ... ],
  ""disclaimer_required"": true,
  ""intent"": ""TRADE"" or ""INFO""
}
```";
        }

        public string ConstructAugmentedUserPrompt(
            string userQuery,
            string portfolioContext,
            string marketContext,
            string ragContext,
            Session session)
        {
            var riskProfile = session?.PortfolioContext?.RiskProfile ?? "moderate";

            return $@"
=== FINANCIAL ADVISOR RAG CONTEXT ===

Current Portfolio:
{portfolioContext}

Market Prices:
{marketContext}

Risk Profile: {riskProfile}

Recent Financial News & Analysis:
{ragContext}

=== END CONTEXT ===

User Query: {userQuery}

Provide your expert analysis and recommendation now.
Start with your detailed reasoning.
FORBIDDEN PHRASES: 'consult a financial advisor', 'seek expert guidance'. Do not use them.
Then, at the very end, output the JSON block.

If recommending a trade:
```json
{{
  ""trades"": [ {{ ""symbol"": ""TICKER"", ""action"": ""BUY/SELL"", ""qty"": 10 }} ],
  ""disclaimer_required"": true,
  ""intent"": ""TRADE""
}}
```

If just answering a question with no trade:
```json
{{
  ""trades"": [],
  ""disclaimer_required"": true,
  ""intent"": ""INFO""
}}
```
";
        }
    }
}
