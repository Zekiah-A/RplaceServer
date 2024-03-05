using Microsoft.EntityFrameworkCore;

namespace HTTPOfficial;

public class DatabaseContext : DbContext
{
    public DbSet<AccountData> Accounts { get; set; }
    public DbSet<AccountBadge> Badges { get; set; }
    public DbSet<Post> Posts { get; set; }


    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite("Data Source = Server.db");
    }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Primary key for Account
        modelBuilder.Entity<AccountData>()
            .HasKey(account => account.Id);
    }
}