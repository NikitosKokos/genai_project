┌─────────────────────────────────────────────────────────────────┐
│ Frontend Chat Widget │
│ User Query: "What should I buy?" │
└────────────────────────┬────────────────────────────────────────┘
│
▼
┌─────────────────────────────────────────────────────────────────┐
│ Backend RAG Orchestration │
├─────────────────────────────────────────────────────────────────┤
│ │
│ 1. Embedding Generation (sentence-transformers) │
│ "What should I buy?" → [0.123, 0.456, ..., 384-dim] │
│ │ │
│ ▼ │
│ 2. Vector Search in MongoDB │
│ Search financial_documents collection │
│ $search aggregation: "cosmosSearch" │
│ Return top 5 semantically similar documents │
│ │ │
│ ▼ │
│ 3. Retrieve Portfolio Context from MongoDB │
│ Query sessions collection for current holdings │
│ Get portfolio_context BSON document │
│ │ │
│ ▼ │
│ 4. Retrieve Market Data from MongoDB │
│ Query market_cache collection for current prices │
│ │ │
│ ┌──────────────┼──────────────┐ │
│ │ │ │ │
│ ▼ ▼ ▼ │
│ Search Portfolio Market Data │
│ Results Context (AAPL: $189, MSFT: $378) │
│ (5 docs) │
│ │ │ │ │
│ └──────────────┼──────────────┘ │
│ ▼ │
│ 5. Aggregate Retrieved Context │
│ Build prompt with financial news + portfolio + market │
│ │ │
│ ▼ │
│ 6. LLM Invocation (Ollama/GPT-4) │
│ Generate financial advice + trade recommendations │
│ │ │
│ ▼ │
│ 7. Parse & Execute Mock Trades │
│ Extract trade JSON from LLM response │
│ Insert into trading_history collection │
│ Update portfolio_snapshots collection │
│ │ │
│ ▼ │
│ 8. Log RAG Query for Analytics │
│ Store query, retrieved docs, execution time │
│ │
└─────────────────────────────────────────────────────────────────┘
│
▼
┌─────────────────────────────────────────────────────────────────┐
│ MongoDB Collections (Local Container) │
├─────────────────────────────────────────────────────────────────┤
│ │
│ ┌────────────────────────────────────────────────────────┐ │
│ │ financial_documents │ │
│ │ - Embedded financial news, educational content │ │
│ │ - Vector field: 384-dim embeddings from │ │
│ │ sentence-transformers │ │
│ │ - Full-text indexed for keyword search │ │
│ └────────────────────────────────────────────────────────┘ │
│ │
│ ┌────────────────────────────────────────────────────────┐ │
│ │ sessions │ │
│ │ - Current session state │ │
│ │ - Preferences, risk profile │ │
│ │ - Nested portfolio_context │ │
│ └────────────────────────────────────────────────────────┘ │
│ │
│ ┌────────────────────────────────────────────────────────┐ │
│ │ portfolio_snapshots │ │
│ │ - Historical portfolio states │ │
│ │ - Holdings BSON array: [{symbol, quantity, cost}] │ │
│ │ - Timestamps for time-series analysis │ │
│ └────────────────────────────────────────────────────────┘ │
│ │
│ ┌────────────────────────────────────────────────────────┐ │
│ │ trading_history │ │
│ │ - Executed mock trades │ │
│ │ - Nested trades array │ │
│ │ - LLM reasoning stored with each trade │ │
│ └────────────────────────────────────────────────────────┘ │
│ │
│ ┌────────────────────────────────────────────────────────┐ │
│ │ market_cache │ │
│ │ - Current market prices │ │
│ │ - Updated from Alpha Vantage (free tier) │ │
│ └────────────────────────────────────────────────────────┘ │
│ │
│ ┌────────────────────────────────────────────────────────┐ │
│ │ rag_queries │ │
│ │ - Query logs for optimization │ │
│ │ - Retrieved document count, execution time │ │
│ └────────────────────────────────────────────────────────┘ │
│ │
└─────────────────────────────────────────────────────────────────┘
