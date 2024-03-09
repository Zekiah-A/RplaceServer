using HTTPOfficial.DataModel;
using Microsoft.EntityFrameworkCore;

namespace HTTPOfficial;

public class DatabaseContext : DbContext
{
    public DbSet<Account> Accounts { get; set; } = null!;
    public DbSet<LinkedUser> LinkedUsers { get; set; }  = null!;
    public DbSet<Badge> Badges { get; set; } = null!;
    public DbSet<Post> Posts { get; set; } = null!;
    public DbSet<Instance> Instances { get; set; } = null!;

    public DatabaseContext() { }
    public DatabaseContext(DbContextOptions<DatabaseContext> options) : base(options) { }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Primary keys
        modelBuilder.Entity<Account>()
            .HasKey(account => account.Id);
        modelBuilder.Entity<Post>()
            .HasKey(post => post.Id);
        modelBuilder.Entity<Instance>()
            .HasKey(instance => instance.Id);
        modelBuilder.Entity<Badge>()
            .HasKey(badge => badge.Id);
        modelBuilder.Entity<LinkedUser>()
            .HasKey(user => user.Id);

        // Unique record properties
        modelBuilder.Entity<Account>()
            .HasIndex(account => account.Username)
            .IsUnique();
        modelBuilder.Entity<Account>()
            .HasIndex(account => account.Email)
            .IsUnique();
        modelBuilder.Entity<Account>()
            .HasIndex(account => account.Token)
            .IsUnique();
        modelBuilder.Entity<Instance>()
            .HasIndex(instance => instance.VanityName)
            .IsUnique();
        modelBuilder.Entity<Post>()
            .HasIndex(post => post.ContentUploadKey)
            .IsUnique();

        // Badges
        modelBuilder.Entity<Account>()
            .HasMany(account => account.Badges)
            .WithOne(badge => badge.Owner)
            .HasForeignKey(badge => badge.OwnerId);
        // Posts
        modelBuilder.Entity<Account>()
            .HasMany(account => account.Posts)
            .WithOne(post => post.Author)
            .HasForeignKey(post => post.AuthorId);
        // Linked users
        modelBuilder.Entity<Account>()
            .HasMany(account => account.LinkedUsers)
            .WithOne(user => user.Account)
            .HasForeignKey(user => user.AccountId);
        // Instances
        modelBuilder.Entity<Account>()
            .HasMany(account => account.Instances)
            .WithOne(instance => instance.Owner)
            .HasForeignKey(instance => instance.OwnerId);
    }
}