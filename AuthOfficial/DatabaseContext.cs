using AuthOfficial.DataModel;
using Microsoft.EntityFrameworkCore;

namespace AuthOfficial;

public class DatabaseContext : DbContext
{
    // Global auth server accounts
    public DbSet<Account> Accounts { get; set; } = null!;
    public DbSet<Badge> Badges { get; set; } = null!;
    public DbSet<AccountPendingVerification> PendingVerifications { get; set; } = null!;
    //public DbSet<AccountRedditAuth> AccountRedditAuths { get; set; } = null!;
    public DbSet<AccountRefreshToken> AccountRefreshTokens { get; set; } = null!;
    public DbSet<CanvasUserRefreshToken> CanvasUserRefreshTokens { get; set; } = null!;


    // Federated canvas/instance accounts
    public DbSet<CanvasUser> CanvasUsers { get; set; } = null!;
    
    public DbSet<Forum> Forums { get; set; } = null!;
    public DbSet<Post> Posts { get; set; } = null!;
    public DbSet<PostContent> PostContents { get; set; } = null!;
    public DbSet<BlockedContent> BlockedContents { get; set; } = null!;
    public DbSet<Instance> Instances { get; set; } = null!;

    public DatabaseContext() { }
    public DatabaseContext(DbContextOptions<DatabaseContext> options) : base(options) { }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Account>(entity =>
        {
            // Primary key            
            entity.HasKey(account => account.Id);

            // Unique & Indexes
            entity
                .HasIndex(account => account.Username)
                .IsUnique();
            entity
                .HasIndex(account => account.Email)
                .IsUnique();
            entity
                .HasIndex(account => account.Status);

            // Badges, Many : One
            entity
                .HasMany(account => account.Badges)
                .WithOne(badge => badge.Owner)
                .HasForeignKey(badge => badge.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);
            // Post author accounts, Many : One 
            entity
                .HasMany(account => account.Posts)
                .WithOne(post => post.AccountAuthor)
                .HasForeignKey(post => post.AccountAuthorId)
                .OnDelete(DeleteBehavior.Restrict);
            // Linked users, One : Many
            entity
                .HasMany(account => account.LinkedUsers)
                .WithOne(user => user.Account)
                .HasForeignKey(user => user.AccountId);
            // Instances, One : Many
            entity
                .HasMany(account => account.Instances)
                .WithOne(instance => instance.Owner)
                .HasForeignKey(instance => instance.OwnerId);
        });

        modelBuilder.Entity<AccountPendingVerification>(entity =>
        {
            // Primary key            
            entity.HasKey(verification => verification.Id);

            // Unique
            entity
                .HasIndex(verification => verification.Code)
                .IsUnique();
            // Required
            entity
                .Property(verification => verification.ExpirationDate)
                .IsRequired();
        });

        modelBuilder.Entity<Forum>(entity =>
        {
            // Primary key
            entity.HasKey(forum => forum.Id);

            entity
                .HasIndex(forum => forum.VanityName)
                .IsUnique();
            entity
                .Property(forum => forum.VanityName)
                .IsRequired();

            entity.HasMany(forum => forum.Posts)
                .WithOne(post => post.Forum)
                .HasForeignKey(post => post.ForumId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Post>(entity =>
        {
            // Primary key
            entity
                .HasKey(post => post.Id);

            // Indexes
            entity
                .HasIndex(post => post.CreationDate);
            entity
                .HasIndex(post => post.Upvotes);
            entity
                .HasIndex(post => post.Downvotes);

            // Post contents, One : Many
            entity
                .HasMany(post => post.Contents)
                .WithOne(content => content.Post)
                .HasForeignKey(content => content.PostId);
            entity
                .Navigation(post => post.Contents)
                .AutoInclude();
        });

        modelBuilder.Entity<Instance>(entity =>
        {
            // Primary key
            entity.HasKey(instance => instance.Id);
            
            // Unique & Required
            entity
                .HasIndex(instance => instance.VanityName)
                .IsUnique();
            entity
                .Property(instance => instance.VanityName)
                .IsRequired();

            // Canvas Users, One : Many
            entity
                .HasMany(instance => instance.Users)
                .WithOne(user => user.Instance)
                .HasForeignKey(user => user.InstanceId);
        });

        modelBuilder.Entity<CanvasUser>(entity =>
        {
            // Primary key
            entity
                .HasKey(user => user.Id);

            // Post author canvas user, Many : One
            entity
                .HasMany(user => user.Posts)
                .WithOne(post => post.CanvasUserAuthor)
                .HasForeignKey(post => post.CanvasUserAuthorId);
        });

        modelBuilder.Entity<PostContent>(entity =>
        {
            // Primary key
            entity
                .HasKey(content => content.Id);

            // Unique post content content key
            entity
                .HasIndex(content => content.ContentKey)
                .IsUnique();
        });

        modelBuilder.Entity<BlockedContent>(entity =>
        {
            // Primary key
            entity
                .HasKey(bannedContent => bannedContent.Id);

            // Banned content moderator, One : Many
            entity
                .HasOne(bannedContent => bannedContent.Moderator)
                .WithMany()
                .HasForeignKey(bannedContent => bannedContent.ModeratorId);
        });
        
        modelBuilder.Entity<Badge>(entity =>
        {
            // Primary key
            entity
                .HasKey(badge => badge.Id);
        });

        modelBuilder.Entity<AccountRefreshToken>(entity =>
        {
            entity.HasKey(refreshToken => refreshToken.Id);

            entity.Property(refreshToken => refreshToken.Token)
                .IsRequired()
                .HasMaxLength(255);

            entity.HasOne(refreshToken => refreshToken.Account)
                .WithMany(account => account.RefreshTokens)
                .HasForeignKey(refreshToken => refreshToken.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CanvasUserRefreshToken>(entity =>
        {
            entity.HasKey(refreshToken => refreshToken.Id);

            entity.Property(refreshToken => refreshToken.Token)
                .IsRequired()
                .HasMaxLength(255);

            entity.HasOne(refreshToken => refreshToken.CanvasUser)
                .WithMany(canvasUser => canvasUser.RefreshTokens)
                .HasForeignKey(refreshToken => refreshToken.CanvasUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}