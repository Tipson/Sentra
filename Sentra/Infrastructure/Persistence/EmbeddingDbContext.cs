// EmbeddingDbContext.cs — EF Core контекст (слой Infrastructure/Persistence)

using Microsoft.EntityFrameworkCore;
using Sentra.Config;
using Sentra.Domain;

namespace Sentra.Infrastructure.Persistence;

public class EmbeddingDbContext : DbContext
{
    public DbSet<FileRecord> Files => Set<FileRecord>();
    public DbSet<SearchHistory> History { get; set; } = null!;

    private readonly string _dbPath;

    public EmbeddingDbContext(string? dbPath = null)
    {
        _dbPath = dbPath ?? AppConfig.DatabasePath;
        Database.EnsureCreated();
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite($"Data Source={_dbPath}");
    }
}