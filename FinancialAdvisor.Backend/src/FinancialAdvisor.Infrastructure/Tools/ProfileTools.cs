using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using FinancialAdvisor.Application.Interfaces;

namespace FinancialAdvisor.Infrastructure.Tools
{
    public class GetProfileTool : ITool
    {
        private readonly IContextService _contextService;

        public GetProfileTool(IContextService contextService)
        {
            _contextService = contextService;
        }

        public string Name => "get_profile";
        public string Description => "Get user profile and portfolio. Args: user_id (string)";

        public async Task<string> ExecuteAsync(string argsJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(argsJson);
                // We accept user_id but interpret it as sessionId for now, 
                // or ideally we would have a mapping. 
                // For this MVP, we assume user_id == sessionId.
                var userId = doc.RootElement.GetProperty("user_id").GetString();

                var session = await _contextService.GetSessionAsync(userId);
                var portfolio = await _contextService.GetPortfolioAsync(userId);

                var strategy = session?.PortfolioContext?.InvestmentGoal ?? "unknown";
                var cash = portfolio?.CashBalance ?? 0;
                
                var holdings = portfolio?.Holdings?.Select(h => new 
                { 
                    symbol = h.Symbol, 
                    qty = h.Quantity 
                }).ToList();
                
                if (holdings == null) holdings = new List<object>().Select(o => new { symbol = "", qty = 0 }).ToList(); // Hacky empty list

                // Better approach:
                var holdingsList = portfolio?.Holdings?.Select(h => new { symbol = h.Symbol, qty = h.Quantity }).ToList() 
                                   ?? new List<object>().Select(x => new { symbol = "", qty = 0 }).ToList(); // Empty anonymous list

                return JsonSerializer.Serialize(new
                {
                    user_id = userId,
                    strategy = strategy,
                    cash = cash,
                    holdings = holdings
                });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { error = ex.Message });
            }
        }
    }

    public class GetOwnedSharesTool : ITool
    {
        private readonly IContextService _contextService;

        public GetOwnedSharesTool(IContextService contextService)
        {
            _contextService = contextService;
        }

        public string Name => "get_owned_shares";
        public string Description => "Get quantity of shares owned for a specific symbol. Args: user_id (string)";

        public async Task<string> ExecuteAsync(string argsJson)
        {
             try
            {
                using var doc = JsonDocument.Parse(argsJson);
                var userId = doc.RootElement.GetProperty("user_id").GetString();

                var portfolio = await _contextService.GetPortfolioAsync(userId);
                
                var holdings = portfolio?.Holdings?.Select(h => new 
                { 
                    symbol = h.Symbol, 
                    qty = h.Quantity 
                }).ToList();

                return JsonSerializer.Serialize(new
                {
                    user_id = userId,
                    holdings = holdings
                });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { error = ex.Message });
            }
        }
    }
}
