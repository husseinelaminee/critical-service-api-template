using Microsoft.EntityFrameworkCore;
using CriticalService.Api.Idempotency;

namespace CriticalService.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<TodoItem> TodoItems => Set<TodoItem>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TodoItem>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Title).HasMaxLength(200).IsRequired();
            b.Property(x => x.Version).IsConcurrencyToken();
        });

        modelBuilder.Entity<IdempotencyRecord>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Key).HasMaxLength(200).IsRequired();
            b.Property(x => x.PathAndQuery).HasMaxLength(500).IsRequired();
            b.Property(x => x.Method).HasMaxLength(10).IsRequired();
            b.Property(x => x.RequestHash).HasMaxLength(128).IsRequired();

            b.HasIndex(x => new { x.Key, x.Method, x.PathAndQuery, x.RequestHash })
             .IsUnique();
        });
    }
}

public class TodoItem
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public bool IsDone { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public int Version { get; set; }

}
