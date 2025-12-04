using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace FinancialAdvisor.Application.Models
{
    // Financial Documents for RAG
    [BsonCollection("financial_documents")]
    public class FinancialDocument
    {
        [BsonId]
        public ObjectId Id { get; set; }

        [BsonElement("title")]
        public string Title { get; set; }

        [BsonElement("content")]
        public string Content { get; set; }

        [BsonElement("source")]
        public string Source { get; set; }

        [BsonElement("category")]
        public string Category { get; set; }

        [BsonElement("embedding")]
        public double[] Embedding { get; set; }  // 384-dimensional vector

        [BsonElement("metadata")]
        public BsonDocument Metadata { get; set; }

        [BsonElement("created_at")]
        public DateTime CreatedAt { get; set; }
    }

    // User Session
    [BsonCollection("sessions")]
    public class Session
    {
        [BsonId]
        public ObjectId Id { get; set; }

        [BsonElement("session_id")]
        public string SessionId { get; set; }

        [BsonElement("created_at")]
        public DateTime CreatedAt { get; set; }

        [BsonElement("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [BsonElement("portfolio_context")]
        public PortfolioContext PortfolioContext { get; set; }

        [BsonElement("preferences")]
        public BsonDocument Preferences { get; set; }
    }

    public class PortfolioContext
    {
        [BsonElement("risk_profile")]
        public string RiskProfile { get; set; }

        [BsonElement("risk_level")]
        public int? RiskLevel { get; set; }

        [BsonElement("investment_goal")]
        public string InvestmentGoal { get; set; }

        [BsonElement("total_portfolio_value")]
        public decimal TotalPortfolioValue { get; set; }
    }

    // Portfolio Snapshots (Historical)
    [BsonCollection("portfolio_snapshots")]
    public class PortfolioSnapshot
    {
        [BsonId]
        public ObjectId Id { get; set; }

        [BsonElement("session_id")]
        public string SessionId { get; set; }

        [BsonElement("holdings")]
        public List<Holding> Holdings { get; set; }

        [BsonElement("total_value")]
        public decimal TotalValue { get; set; }

        [BsonElement("cash_balance")]
        public decimal CashBalance { get; set; }

        [BsonElement("created_at")]
        public DateTime CreatedAt { get; set; }
    }

    public class Holding
    {
        [BsonElement("symbol")]
        public string Symbol { get; set; }

        [BsonElement("quantity")]
        public int Quantity { get; set; }

        [BsonElement("avg_cost")]
        public decimal AvgCost { get; set; }

        [BsonElement("current_price")]
        public decimal CurrentPrice { get; set; }
    }

    // Trading History
    [BsonCollection("trading_history")]
    public class TradingHistory
    {
        [BsonId]
        public ObjectId Id { get; set; }

        [BsonElement("session_id")]
        public string SessionId { get; set; }

        [BsonElement("trades")]
        public List<Trade> Trades { get; set; }
    }

    public class Trade
    {
        [BsonElement("trade_id")]
        public ObjectId TradeId { get; set; } = ObjectId.GenerateNewId();

        [BsonElement("symbol")]
        public string Symbol { get; set; }

        [BsonElement("action")]
        public string Action { get; set; }  // BUY, SELL, HOLD

        [BsonElement("quantity")]
        public int Quantity { get; set; }

        [BsonElement("price")]
        public decimal Price { get; set; }

        [BsonElement("total_amount")]
        public decimal TotalAmount { get; set; }

        [BsonElement("executed_at")]
        public DateTime ExecutedAt { get; set; }

        [BsonElement("reasoning")]
        public string Reasoning { get; set; }  // LLM reasoning
    }

    // Market Data Cache
    [BsonCollection("market_cache")]
    public class MarketDataCache
    {
        [BsonId]
        public ObjectId Id { get; set; }

        [BsonElement("symbol")]
        public string Symbol { get; set; }

        [BsonElement("price")]
        public decimal Price { get; set; }

        [BsonElement("volume")]
        public long Volume { get; set; }

        [BsonElement("change_percent")]
        public decimal ChangePercent { get; set; }

        [BsonElement("last_updated")]
        public DateTime LastUpdated { get; set; }
    }

    // RAG Query Logs
    [BsonCollection("rag_queries")]
    public class RagQuery
    {
        [BsonId]
        public ObjectId Id { get; set; }

        [BsonElement("session_id")]
        public string SessionId { get; set; }

        [BsonElement("query")]
        public string Query { get; set; }

        [BsonElement("retrieved_docs")]
        public int RetrievedDocs { get; set; }

        [BsonElement("response_quality_score")]
        public decimal ResponseQualityScore { get; set; }

        [BsonElement("execution_time_ms")]
        public int ExecutionTimeMs { get; set; }

        [BsonElement("created_at")]
        public DateTime CreatedAt { get; set; }
    }

    // NEW: Asset Catalog for "Available Assets" Panel
    [BsonCollection("assets")]
    public class AssetDefinition
    {
        [BsonId]
        public ObjectId Id { get; set; }

        [BsonElement("symbol")]
        public string Symbol { get; set; }

        [BsonElement("name")]
        public string Name { get; set; }

        [BsonElement("sector")]
        public string Sector { get; set; }

        [BsonElement("type")]
        public string Type { get; set; } // Stock, ETF, Crypto

        [BsonElement("logo_url")]
        public string LogoUrl { get; set; }
        
        [BsonElement("is_active")]
        public bool IsActive { get; set; } = true;
    }

    // NEW: Chat History for UI Persistence
    [BsonCollection("chat_history")]
    public class ChatMessage
    {
        [BsonId]
        public ObjectId Id { get; set; }

        [BsonElement("session_id")]
        public string SessionId { get; set; }

        [BsonElement("role")]
        public string Role { get; set; } // "user" or "assistant"

        [BsonElement("content")]
        public string Content { get; set; }

        [BsonElement("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        [BsonElement("metadata")]
        public BsonDocument Metadata { get; set; }
    }

    // NEW: Portfolio History for Performance Chart
    [BsonCollection("portfolio_history")]
    public class PortfolioHistory
    {
        [BsonId]
        public ObjectId Id { get; set; }

        [BsonElement("session_id")]
        public string SessionId { get; set; }

        [BsonElement("date")]
        public DateTime Date { get; set; }

        [BsonElement("total_value")]
        public decimal TotalValue { get; set; }
    }

    // Attribute to mark collections
    [AttributeUsage(AttributeTargets.Class)]
    public class BsonCollectionAttribute : Attribute
    {
        public string CollectionName { get; }

        public BsonCollectionAttribute(string collectionName)
        {
            CollectionName = collectionName;
        }
    }
}
