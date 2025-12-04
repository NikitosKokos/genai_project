using FinancialAdvisor.Application.Interfaces;
using FinancialAdvisor.Application.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using FinancialAdvisor.Infrastructure.Tools;

namespace FinancialAdvisor.Infrastructure.Services.RAG
{
    public class AgentOrchestrator : IRagService
    {
        private readonly IContextService _contextService;
        private readonly IPromptService _promptService;
        private readonly ILLMService _llmService;
        private readonly IEnumerable<ITool> _tools;
        private readonly ILogger<AgentOrchestrator> _logger;
        
        public AgentOrchestrator(
            IContextService contextService,
            IPromptService promptService,
            ILLMService llmService,
            IEnumerable<ITool> tools,
            ILogger<AgentOrchestrator> logger)
        {
            _contextService = contextService;
            _promptService = promptService;
            _llmService = llmService;
            _tools = tools;
            _logger = logger;
        }

        public async Task<object> QueryAsync(string query)
        {
            return await ProcessQueryAsync(query, "default-session");
        }

        public async Task<ChatResponse> ProcessQueryAsync(string userQuery, string sessionId)
        {
            var responseContent = "";
            await foreach (var chunk in ProcessQueryStreamAsync(userQuery, sessionId))
            {
                // Only keep the final part which isn't a status update if we want to be pure,
                // but for now we just accumulate everything that isn't a status message?
                // Actually, for non-streaming, we want the final clean answer.
                // Since ProcessQueryStreamAsync mixes status and content, this is tricky to reuse directly
                // without parsing.
                // So we keep the logic separate or duplicate for now to be safe, OR we adapt ProcessQueryStreamAsync
                // to yield typed objects (Status vs Token). But the interface is IAsyncEnumerable<string>.
                
                if (!chunk.StartsWith("STATUS:")) 
                {
                    responseContent += chunk;
                }
            }

            return new ChatResponse 
            { 
                Advice = responseContent,
                Timestamp = DateTime.UtcNow
            };
        }

        public async IAsyncEnumerable<string> ProcessQueryStreamAsync(string userQuery, string sessionId, [EnumeratorCancellation] System.Threading.CancellationToken cancellationToken = default, bool enableReasoning = false, int documentCount = 3)
        {
            _logger.LogInformation($"[{sessionId}] Processing Agent Query: {userQuery}");
            yield return "STATUS: Analyzing request...\n";

            // 1. Gather Initial Context
            var history = await _contextService.GetChatHistoryAsync(sessionId, 6);
            var session = await _contextService.GetSessionAsync(sessionId);
            var portfolio = await _contextService.GetPortfolioAsync(sessionId);

            // 2. Proactive RAG
            var ragTool = _tools.FirstOrDefault(t => t.Name == "search_rag");
            string ragContext = "";
            if (ragTool != null)
            {
                yield return "STATUS: Checking knowledge base...\n";
                var ragResultJson = await ragTool.ExecuteAsync(JsonSerializer.Serialize(new { query = userQuery, top_k = 3 }));
                ragContext = ragResultJson;
            }

            // 3. Initial Prompt
            string marketContext = "[]"; 
            string portfolioContext = _contextService.FormatPortfolioContext(portfolio);

            var fullPrompt = _promptService.ConstructAugmentedUserPrompt(
                userQuery, 
                portfolioContext, 
                marketContext, 
                ragContext, 
                session, 
                history);

            yield return "STATUS: Planning...\n";

            // 4. Call LLM (Plan)
            var llmResponse = await _llmService.GenerateFinancialAdviceAsync(userQuery, fullPrompt, sessionId);
            var processedResponse = _promptService.PostProcessModelOutput(llmResponse);

            JsonDocument doc = null;
            JsonElement root = default;
            bool isPlan = false;

            try 
            {
                doc = JsonDocument.Parse(processedResponse);
                root = doc.RootElement;
                if (root.TryGetProperty("type", out var typeProp))
                {
                    var typeStr = typeProp.GetString()?.ToLower();
                    if (typeStr == "plan") isPlan = true;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse Plan JSON. Falling back to raw response.");
                // Cannot yield in catch block
            }

            if (doc == null && !isPlan)
            {
                 yield return processedResponse;
                 yield break;
            }

            if (isPlan)
            {
                // EXECUTE PLAN
                yield return "STATUS: Executing plan...\n";
                
                var steps = root.GetProperty("steps");
                var toolOutputs = new List<string>();

                foreach (var step in steps.EnumerateArray())
                {
                    if (cancellationToken.IsCancellationRequested) yield break;

                    var toolName = step.GetProperty("tool").GetString();
                    var args = step.GetProperty("args");
                    var argsJson = args.ToString();
                    var why = step.TryGetProperty("why", out var whyProp) ? whyProp.GetString() : "";

                    yield return $"STATUS: Calling {toolName}...\n";
                    _logger.LogInformation($"[{sessionId}] Tool Call: {toolName} | Reason: {why}");

                    var tool = _tools.FirstOrDefault(t => t.Name == toolName);
                    if (tool != null)
                    {
                        var output = await tool.ExecuteAsync(argsJson);
                        toolOutputs.Add($"Tool '{toolName}' output: {output}");
                    }
                    else
                    {
                        toolOutputs.Add($"Tool '{toolName}' not found.");
                    }
                }

                // 6. Final Generation
                yield return "STATUS: Finalizing answer...\n";
                var finalPromptInstruction = root.GetProperty("final_prompt").GetString();
                var toolContext = string.Join("\n\n", toolOutputs);
                
                // Stream the final answer token by token
                string finalResponseClean = "";
                await foreach (var token in _llmService.GenerateFinancialAdviceStreamAsync(finalPromptInstruction, toolContext, sessionId, cancellationToken))
                {
                    finalResponseClean += token;
                    yield return token;
                }
                
                // Post-process logic (saving to history) remains, but we use the accumulated response
                // ... existing parsing logic for answer_verbose/answer_plain if needed ...
                // Since we are streaming raw tokens now, we might just save the full text.
                // Or if the model outputs JSON, we stream the JSON raw.
                
                // For history saving, we use finalResponseClean
                
                // Parse Final Answer to extract just the verbose text if it was JSON
                // (This is tricky if we streamed it raw to the user. If we streamed JSON to user, user sees JSON.
                // If we want user to see text, we must parse it. But parsing requires full text.
                // Best practice: Ask LLM for TEXT in the final prompt, not JSON, OR filter JSON on the fly.)
                
                // Given the current prompt asks for JSON "FinalAnswer format", the stream will be JSON.
                // The user probably wants to see the "answer_verbose" content streamed.
                // This is hard to stream token-by-token if it's wrapped in JSON.
                
                // ADJUSTMENT: We will parse the final result for history, but for streaming to user, 
                // if it's JSON, it looks bad.
                // Recommendation: Change the final step to ask for Markdown/Text, NOT JSON.
                // The "Plan" step handles the structured logic. The "Final" step is for the user.
                
                await _contextService.AddChatMessageAsync(sessionId, new ChatMessage { Role = "user", Content = userQuery });
                // We try to clean it up for history if it was JSON
                string historyContent = finalResponseClean;
                try 
                {
                    using var finalDoc = JsonDocument.Parse(finalResponseClean);
                    if (finalDoc.RootElement.TryGetProperty("answer_verbose", out var v))
                    {
                        historyContent = v.GetString();
                    }
                    else if (finalDoc.RootElement.TryGetProperty("answer_plain", out var p))
                    {
                        historyContent = p.GetString();
                    }
                }
                catch { }
                
                await _contextService.AddChatMessageAsync(sessionId, new ChatMessage { Role = "assistant", Content = historyContent });
            }
            else 
            {
                // Direct Answer (FinalAnswer or fallback)
                // If the first pass wasn't a plan, it was a direct answer (likely JSON FinalAnswer).
                // We can't easily stream a JSON object's specific field without a parser that handles partial JSON.
                // For now, we yield the already generated text (block).
                // To support streaming direct answers, we would need to stream the FIRST call too,
                // but we need to parse it to see if it's a PLAN or ANSWER.
                // Streaming + Logic Branching = Complicated.
                // Current "Best Effort": Stream the tools part, then block-return the final answer if it was a plan.
                // OR: Since we are here, we already have the full response `processedResponse`.
                yield return processedResponse; 
                
                // ... history saving ...
                string answer = processedResponse;
                if (root.ValueKind != JsonValueKind.Undefined && root.TryGetProperty("answer_verbose", out var v))
                {
                    answer = v.GetString();
                }
                else if (root.ValueKind != JsonValueKind.Undefined && root.TryGetProperty("answer_plain", out var p))
                {
                    answer = p.GetString();
                }

                await _contextService.AddChatMessageAsync(sessionId, new ChatMessage { Role = "user", Content = userQuery });
                await _contextService.AddChatMessageAsync(sessionId, new ChatMessage { Role = "assistant", Content = answer });
            }

            if (doc != null) doc.Dispose();
        }
    }
}
