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
            return @"You are a helpful financial assistant.

You MUST follow this exact response format for every request:

STEP 1: RESPONSE TO USER
(Write a clear, friendly, and decisive paragraph here explaining your advice to the user. This text MUST appear before the JSON.)

STEP 2: JSON DATA
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
=== CONTEXT ===
Portfolio: {portfolioContext}
Market: {marketContext}
Risk Level: {numericLevel}
News:
{ragContext}
=== END CONTEXT ===

User Query: {userQuery}

MANDATORY INSTRUCTIONS:
1. Answer the user's question DIRECTLY.
2. If the context above is empty or insufficient, USE YOUR GENERAL KNOWLEDGE about the stock market to provide a reasonable answer.
3. DO NOT SAY ""I don't have access"" or ""I cannot"". 
4. Provide a concrete recommendation (Buy, Sell, or Hold) based on the available info or general market trends.
5. Write the RESPONSE TO USER (plain text) first.
6. FINALLY output the JSON DATA.

JSON FORMATS:
Trade: {{ ""trades"": [ {{ ""symbol"": ""TICKER"", ""action"": ""BUY/SELL"", ""qty"": 10 }} ], ""disclaimer_required"": true, ""intent"": ""TRADE"" }}
No Trade: {{ ""trades"": [], ""disclaimer_required"": true, ""intent"": ""INFO"" }}
";
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