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
builder.Services.AddScoped<IContextService, ContextService>();
builder.Services.AddScoped<IMarketDataService, MarketDataService>();
builder.Services.AddScoped<IPromptService, PromptService>();
builder.Services.AddScoped<IActionService, ActionService>();
builder.Services.AddScoped<IRagService, RagOrchestrator>();
builder.Services.AddScoped<IEmbeddingService, EmbeddingService>();
builder.Services.AddScoped<ILLMService, LLMService>();

// Background Services
builder.Services.AddHostedService<NewsIngestionService>();
builder.Services.AddHostedService<DataSeederService>();

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
