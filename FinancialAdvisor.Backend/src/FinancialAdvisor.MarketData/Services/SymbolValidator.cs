namespace FinancialAdvisor.MarketData.Services;

public class SymbolValidator
{
    public bool IsValid(string symbol)
    {
        return !string.IsNullOrWhiteSpace(symbol);
    }
}

