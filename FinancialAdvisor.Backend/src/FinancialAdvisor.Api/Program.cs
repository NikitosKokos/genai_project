using FinancialAdvisor.Infrastructure.Data;
using FinancialAdvisor.Infrastructure.Services;
using FinancialAdvisor.Infrastructure.Services.RAG;
using FinancialAdvisor.Application.Interfaces;
using FinancialAdvisor.Application.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Add MongoDB context
builder.Services.AddSingleton<MongoDbContext>();

// Register repositories and services
builder.Services.AddScoped<IRagService, MongoDBRAGService>();
builder.Services.AddScoped<IEmbeddingService, EmbeddingService>();
builder.Services.AddScoped<ILLMService, LLMService>();
builder.Services.AddScoped<IMarketDataService, MarketDataService>();

// Add HTTP client for external APIs
builder.Services.AddHttpClient();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection(); // Often disabled in internal docker networks or dev

app.UseAuthorization();

app.MapControllers();

app.Run();
