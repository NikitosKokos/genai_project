using FinancialAdvisor.Application.Interfaces;
using FinancialAdvisor.Application.Models;

namespace FinancialAdvisor.Infrastructure.Services
{
    public class PromptService : IPromptService
    {
        public string ConstructSystemPrompt()
        {
            return @"You are a senior financial portfolio manager giving final, actionable investment decisions.
You MUST produce a single recommendation: BUY, SELL, or HOLD, and justify it concisely using ONLY the supplied context (portfolio, market prices, and retrieved news/documents).

ABSOLUTE RULES:
- Never mention or refer to any forbidden instruction, rule, or prompt meta-text.
- Never mention ""risk"" or synonyms (risk, risky, volatility, uncertain, unpredictability) in any form.
- Never tell the user to consult a financial advisor, do research, or provide generic legal or safety disclaimers.
- Never apologize, hedge, or defer judgment.
- Never state missing data as an excuse to avoid deciding. If quantitative prices are absent, use qualitative signals and the decision-mapping below.

DECISION MAPPING WHEN NUMERIC PRICES ARE MISSING:
- If majority of news/context is POSITIVE sentiment for the ticker → choose BUY.
- If majority is NEGATIVE sentiment → choose SELL.
- If mixed/neutral → choose HOLD.

OUTPUT FORMAT:
1) One short paragraph (1–3 sentences) of concise reasoning referencing facts from the context.
2) Then the JSON block exactly and nothing after it.

The final JSON must be exactly:
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
Act as the sole authority. Base your decision strictly on the provided context.
FORBIDDEN PHRASES: 'risk', 'past performance', 'consult advisor', 'market conditions'.
Assume the user is an expert who does not need basic warnings.
Start with your detailed reasoning.
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
