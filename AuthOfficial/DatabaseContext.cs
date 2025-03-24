using AuthOfficial.DataModel;
using Microsoft.EntityFrameworkCore;

namespace AuthOfficial;

public class DatabaseContext : DbContext
{
    // Global auth server accounts
    public DbSet<Account> Accounts { get; set; } = null!;
    public DbSet<AccountBadge> Badges { get; set; } = null!;
    public DbSet<AccountPendingVerification> PendingVerifications { get; set; } = null!;
    public DbSet<AccountRefreshToken> AccountRefreshTokens { get; set; } = null!;


    // Federated canvas/instance accounts
    public DbSet<CanvasUser> CanvasUsers { get; set; } = null!;
    public DbSet<CanvasUserRefreshToken> CanvasUserRefreshTokens { get; set; } = null!;

    // Forums & Posts
    public DbSet<Forum> Forums { get; set; } = null!;
    public DbSet<Post> Posts { get; set; } = null!;
    public DbSet<PostContent> PostContents { get; set; } = null!;
    public DbSet<BlockedContent> BlockedContents { get; set; } = null!;
    
    // Instances
    public DbSet<Instance> Instances { get; set; } = null!;

    public DatabaseContext() { }
    public DatabaseContext(DbContextOptions<DatabaseContext> options) : base(options) { }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // TPC mapping for Accounts/Canvas users
        // https://learn.microsoft.com/en-us/ef/core/modeling/inheritance#table-per-concrete-type-configuration
        modelBuilder.Entity<AuthBase>().UseTpcMappingStrategy();
        modelBuilder.Entity<Account>().ToTable("Accounts");
        modelBuilder.Entity<CanvasUser>().ToTable("CanvasUsers");

        // Define a shared sequence for AuthBase.Id (PostgreSQL)
        modelBuilder
            .HasSequence<int>("AuthBaseIdsSequence")
            .StartsAt(1)
            .IncrementsBy(1);

        modelBuilder.Entity<Account>(entity =>
        {
            entity.Property(account => account.Id)
                .HasDefaultValueSql("nextval('\"AuthBaseIdsSequence\"')");
            
            // Property constraints
            entity.Property(account => account.Username)
                .HasMaxLength(16);
            entity.Property(account => account.DiscordHandle)
                .HasMaxLength(32);
            entity.Property(account => account.TwitterHandle)
                .HasMaxLength(15);
            entity.Property(account => account.RedditHandle)
                .HasMaxLength(20);
            entity.Property(account => account.Biography)
                .HasMaxLength(360);
            entity.Property(account => account.Email)
                .HasMaxLength(320);

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
            // Linked users, One : Many
            entity
                .HasMany(account => account.LinkedUsers)
                .WithOne(user => user.LinkedAccount)
                .HasForeignKey(user => user.LinkedAccountId);
            // Instances, One : Many
            entity
                .HasMany(account => account.Instances)
                .WithOne(instance => instance.Owner)
                .HasForeignKey(instance => instance.OwnerId);
        });

        modelBuilder.Entity<AccountBadge>(entity =>
        {
            // Primary key
            entity
                .HasKey(badge => badge.Id);
        });

        modelBuilder.Entity<AccountRefreshToken>(entity =>
        {
            entity.HasKey(refreshToken => refreshToken.Id);

            entity
                .Property(refreshToken => refreshToken.Token)
                .IsRequired()
                .HasMaxLength(255);

            entity.HasOne(refreshToken => refreshToken.Account)
                .WithMany(account => account.RefreshTokens)
                .HasForeignKey(refreshToken => refreshToken.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CanvasUser>(entity =>
        {
            entity.Property(canvasUser => canvasUser.Id)
                .HasDefaultValueSql("nextval('\"AuthBaseIdsSequence\"')");
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
            //entity
            //    .HasIndex(post => post.Type);

            // Post contents, One : Many
            entity
                .HasMany(post => post.Contents)
                .WithOne(content => content.Post)
                .HasForeignKey(content => content.PostId);

            // Include contents navigation
            entity
                .Navigation(post => post.Contents)
                .AutoInclude();
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
    }
}