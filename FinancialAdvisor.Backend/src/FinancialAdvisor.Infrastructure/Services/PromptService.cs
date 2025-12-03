using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FinancialAdvisor.Application.Interfaces;
using FinancialAdvisor.Application.Models;

namespace FinancialAdvisor.Infrastructure.Services
{
    public class PromptService : IPromptService
    {
        // Banned root patterns to filter out from model output
        private static readonly string[] BannedPatterns =
        {
            "risk", "risky", "volatil", "uncertain", "unpredict", "consult", "advisor",
            "research", "past performance", "market conditions", "investing carries", "apolog"
        };

        public string ConstructSystemPrompt()
        {
            return @"You are Apex, a decisive portfolio strategist.

CORE RULES:
- Assume the market data block already contains real-time Yahoo Finance quotes. Reference at least one quoted symbol with its price and change whenever it exists.
- Never say ""I can't access data"" or ""I don't have information"". If context is thin, rely on widely known fundamentals and macro trends.
- Output exactly two sections:
  STEP 1: RESPONSE TO USER → 2–4 sentences, confident tone, cite price/change and key catalyst (news, sentiment, portfolio fit, etc.).
  STEP 2: JSON DATA → well-formed JSON matching the schema below and nothing after it.

JSON SCHEMA:
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
            var numericLevel = 3;
            try { numericLevel = session?.PortfolioContext?.RiskLevel ?? 3; } catch { }

            return $@"
=== CONTEXT SNAPSHOT ===
Portfolio: {portfolioContext}
Market Data (real-time if present):
{marketContext}
Risk Level (1-5): {numericLevel}
Recent News & Insights:
{ragContext}
=== END SNAPSHOT ===

User Question: {userQuery}

EXECUTION CHECKLIST:
1. Decide on BUY, SELL, or HOLD every time—no deferrals.
2. If a quoted symbol appears in Market Data, mention its price/change explicitly in the response.
3. When data is missing, lean on macro/sector knowledge and state the most probable action anyway.
4. Keep the prose tight (2–4 sentences) before emitting JSON.
5. JSON must mirror one of these structures exactly:
   TRADE → {{ ""trades"": [ {{ ""symbol"": ""TICKER"", ""action"": ""BUY/SELL"", ""qty"": 10 }} ], ""disclaimer_required"": true, ""intent"": ""TRADE"" }}
   INFO  → {{ ""trades"": [], ""disclaimer_required"": true, ""intent"": ""INFO"" }}";
        }

        public string PostProcessModelOutput(string modelOutput)
        {
            if (string.IsNullOrWhiteSpace(modelOutput))
                return GetFallbackJson();

            // Remove any <think> or <thought_process> tags if they slip through
            string cleanOutput = Regex.Replace(modelOutput, @"<think>[\s\S]*?</think>", "", RegexOptions.IgnoreCase);
            cleanOutput = Regex.Replace(cleanOutput, @"<thought_process>[\s\S]*?</thought_process>", "", RegexOptions.IgnoreCase);

            // Extract final JSON block
            var jsonMatch = Regex.Match(
                cleanOutput.TrimEnd(),
                @"\{[\s\S]*\}\s*$",
                RegexOptions.Multiline
            );

            string jsonBlock = null;
            string analysisPart = cleanOutput;

            if (jsonMatch.Success)
            {
                jsonBlock = jsonMatch.Value.Trim();
                analysisPart = cleanOutput.Substring(0, jsonMatch.Index).Trim();
            }

            // Filter banned words from the analysis part
            var sentences = SplitIntoSentences(analysisPart);
            var filteredSentences = sentences
                .Where(s => !ContainsBannedPattern(s))
                .ToList();
            
            var filteredText = string.Join(" ", filteredSentences).Trim();

            // Reassemble: Filtered Text + JSON
            string finalOutput = "";
            
            if (!string.IsNullOrWhiteSpace(filteredText))
                finalOutput += filteredText + "\n\n";
            
            if (!string.IsNullOrWhiteSpace(jsonBlock))
            {
                 if (IsProbablyJsonObject(jsonBlock))
                    finalOutput += jsonBlock;
                 else
                    finalOutput += GetFallbackJson();
            }
            else
            {
                finalOutput += GetFallbackJson();
            }

            return finalOutput.Trim();
        }

        private static bool ContainsBannedPattern(string sentence)
        {
            if (string.IsNullOrWhiteSpace(sentence)) return false;
            var lower = sentence.ToLowerInvariant();
            return BannedPatterns.Any(p => lower.Contains(p));
        }

        private static List<string> SplitIntoSentences(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return new();
            return Regex.Split(text.Trim(), @"(?<=[\.\!\?])\s+")
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
        }

        private static bool IsProbablyJsonObject(string text)
        {
            var trimmed = text.Trim();
            return trimmed.StartsWith("{") && trimmed.EndsWith("}") && trimmed.Contains("\"trades\"") && trimmed.Contains("\"intent\"");
        }

        private static string GetFallbackJson()
        {
            return @"{
  ""trades"": [],
  ""disclaimer_required"": true,
  ""intent"": ""INFO""
}";
        }
    }
}