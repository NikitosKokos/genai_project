using Xunit;
using Moq;
using FinancialAdvisor.Infrastructure.Services.RAG;
using FinancialAdvisor.Application.Interfaces;
using FinancialAdvisor.Application.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;
using System.Linq;

namespace FinancialAdvisor.UnitTests.Services
{
    public class AgentOrchestratorTests
    {
        private readonly Mock<IContextService> _mockContextService;
        private readonly Mock<IPromptService> _mockPromptService;
        private readonly Mock<ILLMService> _mockLlmService;
        private readonly Mock<ILogger<AgentOrchestrator>> _mockLogger;
        private readonly Mock<ITool> _mockPriceTool;
        private readonly List<ITool> _tools;
        private readonly AgentOrchestrator _orchestrator;

        public AgentOrchestratorTests()
        {
            _mockContextService = new Mock<IContextService>();
            _mockPromptService = new Mock<IPromptService>();
            _mockLlmService = new Mock<ILLMService>();
            _mockLogger = new Mock<ILogger<AgentOrchestrator>>();
            
            _mockPriceTool = new Mock<ITool>();
            _mockPriceTool.Setup(t => t.Name).Returns("get_stock_price");
            _mockPriceTool.Setup(t => t.Description).Returns("Get price");

            _tools = new List<ITool> { _mockPriceTool.Object };

            _orchestrator = new AgentOrchestrator(
                _mockContextService.Object,
                _mockPromptService.Object,
                _mockLlmService.Object,
                _tools,
                _mockLogger.Object
            );
        }

        [Fact]
        public async Task ProcessQueryAsync_ExecutesPlanAndReturnsResponse()
        {
            // Arrange
            var query = "What is AAPL price?";
            var sessionId = "demo_session_001";

            // 1. Context Mocks
            _mockContextService.Setup(s => s.GetChatHistoryAsync(sessionId, 6))
                .ReturnsAsync(new List<ChatMessage>());
            _mockContextService.Setup(s => s.GetSessionAsync(sessionId))
                .ReturnsAsync(new Session());
            _mockContextService.Setup(s => s.GetPortfolioAsync(sessionId))
                .ReturnsAsync(new PortfolioSnapshot());
            _mockContextService.Setup(s => s.FormatPortfolioContext(It.IsAny<PortfolioSnapshot>()))
                .Returns("Portfolio: Empty");

            // 2. Prompt Mocks
            _mockPromptService.Setup(p => p.ConstructAugmentedUserPrompt(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Session>(), It.IsAny<List<ChatMessage>>()))
                .Returns("Augmented Prompt");

            // 3. LLM Planning Mock
            var planJson = JsonSerializer.Serialize(new
            {
                type = "plan",
                steps = new[] 
                { 
                    new { action = "call_tool", tool = "get_stock_price", args = new { symbol = "AAPL" }, why = "Need price" } 
                },
                final_prompt = "Summarize price"
            });

            // First LLM call returns the Plan
            _mockLlmService.SetupSequence(l => l.GenerateFinancialAdviceAsync(It.IsAny<string>(), It.IsAny<string>(), sessionId))
                .ReturnsAsync(planJson) // 1st call: Plan
                .ReturnsAsync(JsonSerializer.Serialize(new { type = "final_answer", answer_plain = "AAPL is $150", answer_verbose = "AAPL is $150..." })); // 2nd call: Final Answer

            _mockPromptService.Setup(p => p.PostProcessModelOutput(It.IsAny<string>()))
                .Returns<string>(s => s); // Pass through

            // 4. Tool Mock
            _mockPriceTool.Setup(t => t.ExecuteAsync(It.IsAny<string>()))
                .ReturnsAsync("{\"price\": 150}");

            // Act
            var response = await _orchestrator.ProcessQueryAsync(query, sessionId);

            // Assert
            // Since we stream status updates now, the Advice accumulates "STATUS: ...\n" lines.
            // We need to check if the final part contains the answer.
            Assert.Contains("AAPL is $150...", response.Advice);
            
            // Verify Tool execution
            _mockPriceTool.Verify(t => t.ExecuteAsync(It.Is<string>(s => s.Contains("AAPL"))), Times.Once);
            
            // Verify History saved
            _mockContextService.Verify(s => s.AddChatMessageAsync(sessionId, It.IsAny<ChatMessage>()), Times.Exactly(2)); // User + Assistant
        }
    }
}

