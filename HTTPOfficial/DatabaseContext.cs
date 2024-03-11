using HTTPOfficial.DataModel;
using Microsoft.EntityFrameworkCore;

namespace HTTPOfficial;

public class DatabaseContext : DbContext
{
    // Global auth server accounts
    public DbSet<Account> Accounts { get; set; } = null!;
    // Federated canvas/instance accounts
    public DbSet<CanvasUser> CanvasUsers { get; set; } = null!;
    public DbSet<Badge> Badges { get; set; } = null!;
    public DbSet<Post> Posts { get; set; } = null!;
    public DbSet<PostContent> PostContents { get; set; } = null!;
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
        modelBuilder.Entity<CanvasUser>()
            .HasKey(user => user.Id);
        modelBuilder.Entity<PostContent>()
            .HasKey(content => content.Id);

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
        modelBuilder.Entity<CanvasUser>()
            .HasIndex(user => user.UserIntId)
            .IsUnique();
        modelBuilder.Entity<PostContent>()
            .HasIndex(content => content.ContentKey)
            .IsUnique();

        // Badges
        modelBuilder.Entity<Account>()
            .HasMany(account => account.Badges)
            .WithOne(badge => badge.Owner)
            .HasForeignKey(badge => badge.OwnerId);
        // Post auth accounts
        modelBuilder.Entity<Account>()
            .HasMany(account => account.Posts)
            .WithOne(post => post.Account)
            .HasForeignKey(post => post.AccountId);
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
        // Post contents
        modelBuilder.Entity<Post>()
            .HasMany(post => post.Contents)
            .WithOne(content => content.Post)
            .HasForeignKey(content => content.PostId);
        // Instance
        modelBuilder.Entity<Instance>()
            .HasMany(instance => instance.Users)
            .WithOne(user => user.Instance)
            .HasForeignKey(user => user.InstanceId);
        // Post canvas user accounts
        modelBuilder.Entity<CanvasUser>()
            .HasMany(user => user.Posts)
            .WithOne(post => post.CanvasUser)
            .HasForeignKey(post => post.CanvasUserId);
    }
}