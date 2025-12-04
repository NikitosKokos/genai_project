using Xunit;
using FinancialAdvisor.Infrastructure.Services;
using FinancialAdvisor.Application.Models;
using FinancialAdvisor.Domain.Entities;
using System.Collections.Generic;

namespace FinancialAdvisor.UnitTests.Services
{
    public class PromptServiceTests
    {
        private readonly PromptService _promptService;

        public PromptServiceTests()
        {
            _promptService = new PromptService();
        }

        [Fact]
        public void ConstructAugmentedUserPrompt_ShouldIncludeMarketContext_WhenProvided()
        {
            // Arrange
            var userQuery = "Should I buy Microsoft?";
            var portfolioContext = "Current Portfolio: Empty";
            var marketContext = "MSFT: $400.00 (+1.2%)";
            var ragContext = "Recent news: MSFT AI integration successful.";
            var session = new Session 
            { 
                PortfolioContext = new PortfolioContext 
                { 
                    RiskLevel = 3,
                    RiskProfile = "Moderate"
                } 
            };

            // Act
            var result = _promptService.ConstructAugmentedUserPrompt(
                userQuery,
                portfolioContext,
                marketContext,
                ragContext,
                session
            );

            // Assert
            Assert.Contains("[MARKET PRICES (Real-Time)]", result);
            Assert.Contains(marketContext, result);
        }

        [Fact]
        public void ConstructAugmentedUserPrompt_ShouldIncludeEmptySection_WhenMarketContextIsMissing()
        {
            // Arrange
            var userQuery = "Analysis please";
            var portfolioContext = "Empty";
            string marketContext = ""; // Empty
            var ragContext = "News...";
            var session = new Session();

            // Act
            var result = _promptService.ConstructAugmentedUserPrompt(
                userQuery,
                portfolioContext,
                marketContext,
                ragContext,
                session
            );

            // Assert
            Assert.Contains("[MARKET PRICES (Real-Time)]", result);
        }

        [Fact]
        public void ConstructAugmentedUserPrompt_StructureVerification()
        {
             // Arrange
            var userQuery = "Query";
            var marketContext = "Market Data";
            var session = new Session();

            // Act
            var result = _promptService.ConstructAugmentedUserPrompt(
                userQuery,
                "Port",
                marketContext,
                "Rag",
                session
            );

            // Assert - verifying the order/sections exist
            Assert.Contains("=== CONTEXT DATA ===", result);
            Assert.Contains("[USER PROFILE]", result);
            Assert.Contains("[PORTFOLIO]", result);
            Assert.Contains("[MARKET PRICES (Real-Time)]", result);
            Assert.Contains("[RELEVANT NEWS (RAG)]", result);
            Assert.Contains("[CHAT HISTORY (Last 6)]", result);
        }
    }
}
