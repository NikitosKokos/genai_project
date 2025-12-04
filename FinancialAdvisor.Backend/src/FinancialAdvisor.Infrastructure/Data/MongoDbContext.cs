using MongoDB.Driver;
using FinancialAdvisor.Application.Models;
using Microsoft.Extensions.Configuration;

namespace FinancialAdvisor.Infrastructure.Data
{
    public class MongoDbContext
    {
        private readonly IMongoDatabase _database;

        public MongoDbContext(IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("MongoDB");
            var client = new MongoClient(connectionString);
            var mongoUrl = MongoUrl.Create(connectionString);
            var databaseName = mongoUrl.DatabaseName ?? "financial_advisor";
            _database = client.GetDatabase(databaseName);
        }

        public IMongoCollection<FinancialDocument> FinancialDocuments =>
            _database.GetCollection<FinancialDocument>("financial_documents");

        public IMongoCollection<Session> Sessions =>
            _database.GetCollection<Session>("sessions");

        public IMongoCollection<PortfolioSnapshot> PortfolioSnapshots =>
            _database.GetCollection<PortfolioSnapshot>("portfolio_snapshots");

        public IMongoCollection<TradingHistory> TradingHistory =>
            _database.GetCollection<TradingHistory>("trading_history");

        public IMongoCollection<MarketDataCache> MarketCache =>
            _database.GetCollection<MarketDataCache>("market_cache");

        public IMongoCollection<RagQuery> RagQueries =>
            _database.GetCollection<RagQuery>("rag_queries");

        public IMongoCollection<AssetDefinition> Assets =>
            _database.GetCollection<AssetDefinition>("assets");

        public IMongoCollection<ChatMessage> ChatHistory =>
            _database.GetCollection<ChatMessage>("chat_history");
    }
}

