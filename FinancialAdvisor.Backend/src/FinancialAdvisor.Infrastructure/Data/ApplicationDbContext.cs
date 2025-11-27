using Microsoft.EntityFrameworkCore;
using FinancialAdvisor.Domain.Entities;

namespace FinancialAdvisor.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<UserProfile> UserProfiles { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<Portfolio> Portfolios { get; set; }
    public DbSet<MarketDataSnapshot> MarketDataSnapshots { get; set; }
}

