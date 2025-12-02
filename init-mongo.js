// init-mongo.js - Runs on MongoDB startup
// Create database and collections with indexes

db = db.getSiblingDB('financial_advisor');

// 1. Create financial_documents collection
db.createCollection('financial_documents');

db.financial_documents.insertMany([
  {
    _id: new ObjectId(),
    title: "Tech stocks surge on AI optimism",
    content: "Major technology companies rally as investors eye generative AI opportunities. AAPL and MSFT lead gains.",
    source: "dummy_news_feed",
    category: "Technology",
    embedding: Array(384).fill(0).map(() => Math.random()),  // Placeholder: will be filled by app
    metadata: {
      sentiment: "positive",
      relevance_score: 0.95,
      symbols: ["AAPL", "MSFT"],
      date: new Date()
    },
    created_at: new Date()
  },
  {
    _id: new ObjectId(),
    title: "Fed signals potential rate cuts",
    content: "Federal Reserve officials hint at economic slowdown, fueling speculation about future rate adjustments.",
    source: "dummy_news_feed",
    category: "Macroeconomics",
    embedding: Array(384).fill(0).map(() => Math.random()),
    metadata: {
      sentiment: "neutral",
      relevance_score: 0.88,
      symbols: ["SPY", "QQQ"],
      date: new Date()
    },
    created_at: new Date()
  }
]);

// Create text index for full-text search
db.financial_documents.createIndex({ title: "text", content: "text" });
// Create index on embedding field (for vector search simulation)
db.financial_documents.createIndex({ embedding: 1 });

console.log("✓ financial_documents collection created with indexes");

// 2. Create sessions collection
db.createCollection('sessions');

db.sessions.insertOne({
  _id: new ObjectId(),
  session_id: "demo_session_001",
  created_at: new Date(),
  updated_at: new Date(),
  portfolio_context: {
    risk_profile: "moderate",
    investment_goal: "long_term_growth",
    total_portfolio_value: 50000
  },
  preferences: {
    preferred_sectors: ["Technology", "Healthcare", "Finance"],
    max_position_size: 0.20,
    rebalance_threshold: 0.10
  }
});

db.sessions.createIndex({ session_id: 1 }, { unique: true });
console.log("✓ sessions collection created");

// 3. Create portfolio_snapshots collection
db.createCollection('portfolio_snapshots');

db.portfolio_snapshots.insertOne({
  _id: new ObjectId(),
  session_id: "demo_session_001",
  holdings: [
    { symbol: "AAPL", quantity: 25, avg_cost: 165.50, current_price: 189.95 },
    { symbol: "MSFT", quantity: 15, avg_cost: 320.00, current_price: 378.91 },
    { symbol: "GOOGL", quantity: 10, avg_cost: 140.00, current_price: 155.23 }
  ],
  total_value: 12500.00,
  cash_balance: 5000.00,
  created_at: new Date()
});

db.portfolio_snapshots.createIndex({ session_id: 1, created_at: -1 });
console.log("✓ portfolio_snapshots collection created");

// 4. Create trading_history collection
db.createCollection('trading_history');

db.trading_history.insertOne({
  _id: new ObjectId(),
  session_id: "demo_session_001",
  trades: [
    {
      trade_id: new ObjectId(),
      symbol: "AAPL",
      action: "BUY",
      quantity: 10,
      price: 185.50,
      total_amount: 1855.00,
      executed_at: new Date(Date.now() - 86400000),  // Yesterday
      reasoning: "Strong technical support at 185 level, positive earnings outlook"
    },
    {
      trade_id: new ObjectId(),
      symbol: "MSFT",
      action: "HOLD",
      quantity: 0,
      price: 0,
      total_amount: 0,
      executed_at: new Date(),
      reasoning: "Current position performing well, no action needed"
    }
  ]
});

db.trading_history.createIndex({ session_id: 1, "trades.executed_at": -1 });
console.log("✓ trading_history collection created");

// 5. Create market_cache collection
db.createCollection('market_cache');

db.market_cache.insertMany([
  {
    _id: new ObjectId(),
    symbol: "AAPL",
    price: 189.95,
    volume: 52000000,
    change_percent: 1.33,
    last_updated: new Date()
  },
  {
    _id: new ObjectId(),
    symbol: "MSFT",
    price: 378.91,
    volume: 18500000,
    change_percent: 1.39,
    last_updated: new Date()
  },
  {
    _id: new ObjectId(),
    symbol: "GOOGL",
    price: 155.23,
    volume: 21300000,
    change_percent: 0.95,
    last_updated: new Date()
  }
]);

db.market_cache.createIndex({ symbol: 1 }, { unique: true });
console.log("✓ market_cache collection created");

// 6. Create rag_queries collection (for analytics)
db.createCollection('rag_queries');

db.rag_queries.insertOne({
  _id: new ObjectId(),
  session_id: "demo_session_001",
  query: "What should I buy in tech sector?",
  retrieved_docs: 3,
  response_quality_score: 0.92,
  execution_time_ms: 245,
  created_at: new Date()
});

db.rag_queries.createIndex({ session_id: 1, created_at: -1 });
console.log("✓ rag_queries collection created");

console.log("\n✅ MongoDB initialization complete!");
console.log("Database: financial_advisor");
console.log("Collections: financial_documents, sessions, portfolio_snapshots, trading_history, market_cache, rag_queries");

