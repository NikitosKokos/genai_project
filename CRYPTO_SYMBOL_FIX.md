# Crypto Symbol Format Fix

## 🐛 Problem

The LLM was calling `get_stock_price` with incorrect crypto symbols:
- Used `"ETH"` instead of `"ETH-USD"` → Returned wrong price ($29.31 instead of ~$3000)
- Used `"BTC"` instead of `"BTC-USD"` → Would return wrong price

The tool description wasn't explicit enough, and the prompt didn't emphasize the correct format.

## ✅ Solution

### 1. **Added Symbol Normalization in Tool**

**File**: `MarketTools.cs`

Added `NormalizeSymbol()` method that automatically converts:
- `"ETH"` → `"ETH-USD"`
- `"BTC"` → `"BTC-USD"`
- `"BITCOIN"` → `"BTC-USD"`
- `"ETHEREUM"` → `"ETH-USD"`
- And other common crypto abbreviations

**Benefits**:
- Handles both formats (abbreviation and full format)
- Prevents errors if LLM uses wrong format
- More robust and user-friendly

### 2. **Enhanced Tool Description**

**Before**:
```
"Get current stock or crypto price. Args: symbol (string) - supports both stocks (e.g., AAPL) and crypto (e.g., BTC-USD)"
```

**After**:
```
"Get current stock or crypto price. Args: symbol (string) - For stocks use ticker (e.g., AAPL, MSFT). For crypto, use full symbol with -USD suffix (e.g., BTC-USD for Bitcoin, ETH-USD for Ethereum). IMPORTANT: For Bitcoin use 'BTC-USD', for Ethereum use 'ETH-USD'. Do NOT use 'BTC' or 'ETH' alone."
```

### 3. **Updated System Prompt**

**Added explicit crypto symbol rules**:
- Rule 6 now emphasizes: Use `"BTC-USD"` (NOT `"BTC"`), Use `"ETH-USD"` (NOT `"ETH"`)
- Tool contract section includes detailed symbol format rules
- Added examples in Plan JSON format showing correct usage

**Changes**:
```csharp
6. If asked for current market price and a fresh price tool was not provided, instruct the orchestrator to call `get_stock_price(symbol)`. 
   **CRITICAL for crypto symbols**: Use the full symbol format with -USD suffix:
   - Bitcoin: use "BTC-USD" (NOT "BTC")
   - Ethereum: use "ETH-USD" (NOT "ETH")
   - Other crypto: use "SYMBOL-USD" format
```

### 4. **Improved Error Messages**

Error messages now:
- Show the normalized symbol if it was changed
- Provide helpful guidance about correct format
- Example: `"Symbol 'ETH' (normalized to 'ETH-USD') not found. For crypto, ensure you use the full format like 'BTC-USD' or 'ETH-USD'."`

## 📊 Code Changes

### `MarketTools.cs`

```csharp
private string NormalizeSymbol(string symbol)
{
    // Maps common crypto abbreviations to full symbols
    var cryptoMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "BTC", "BTC-USD" },
        { "BITCOIN", "BTC-USD" },
        { "ETH", "ETH-USD" },
        { "ETHEREUM", "ETH-USD" },
        // ... more mappings
    };
    
    // Returns normalized symbol
}
```

### `PromptService.cs`

- Enhanced Rule 6 with crypto symbol format requirements
- Added detailed symbol format rules in tool contract
- Added examples in Plan JSON format

## 🧪 Testing

The fix ensures:
1. ✅ LLM can use `"ETH"` or `"ETH-USD"` → Both work (normalized to `"ETH-USD"`)
2. ✅ LLM can use `"BTC"` or `"BTC-USD"` → Both work (normalized to `"BTC-USD"`)
3. ✅ Prompt explicitly tells LLM to use full format
4. ✅ Tool description is clear about format requirements
5. ✅ Error messages guide LLM to correct format

## 🎯 Best Practices Followed

1. **Defensive Programming**: Tool handles both formats gracefully
2. **Clear Documentation**: Tool description and prompt are explicit
3. **User-Friendly**: Normalization prevents errors
4. **Helpful Errors**: Error messages guide to correct format
5. **Industry Standards**: Follows common crypto symbol conventions

## 📝 Result

Now when user asks:
- "What is ETH price?" → LLM calls `get_stock_price("ETH-USD")` or `get_stock_price("ETH")` → Both normalized to `"ETH-USD"` → Returns correct price (~$3000)
- "What is Bitcoin price?" → LLM calls `get_stock_price("BTC-USD")` or `get_stock_price("BTC")` → Both normalized to `"BTC-USD"` → Returns correct price (~$90000)

The system is now robust and handles both correct and incorrect formats gracefully! ✅

