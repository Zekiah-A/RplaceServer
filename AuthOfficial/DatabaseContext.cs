using AuthOfficial.DataModel;
using Microsoft.EntityFrameworkCore;

namespace AuthOfficial;

public class DatabaseContext : DbContext
{
    // Global auth server accounts
    public DbSet<Account> Accounts { get; set; } = null!;
    public DbSet<AccountPendingVerification> PendingVerifications { get; set; } = null!;
    //public DbSet<AccountRedditAuth> AccountRedditAuths { get; set; } = null!;
    public DbSet<AccountRefreshToken> AccountRefreshTokens { get; set; } = null!;
    public DbSet<CanvasUserRefreshToken> CanvasUserRefreshTokens { get; set; } = null!;


    // Federated canvas/instance accounts
    public DbSet<CanvasUser> CanvasUsers { get; set; } = null!;
    
    public DbSet<Badge> Badges { get; set; } = null!;
    public DbSet<Forum> Forums { get; set; } = null!;
    public DbSet<Post> Posts { get; set; } = null!;
    public DbSet<PostContent> PostContents { get; set; } = null!;
    public DbSet<BannedContent> BlockedContents { get; set; } = null!;
    public DbSet<Instance> Instances { get; set; } = null!;

    public DatabaseContext() { }
    public DatabaseContext(DbContextOptions<DatabaseContext> options) : base(options) { }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Primary keys
        modelBuilder.Entity<Account>()
            .HasKey(account => account.Id);
        modelBuilder.Entity<AccountPendingVerification>()
            .HasKey(verification => verification.Id);
        //modelBuilder.Entity<AccountRedditAuth>()
        //    .HasKey(redditAuth => redditAuth.Id);
        modelBuilder.Entity<Post>()
            .HasKey(post => post.Id);
        modelBuilder.Entity<Instance>()
            .HasKey(instance => instance.Id);
        modelBuilder.Entity<Instance>()
            .HasKey(instance => instance.Id);
        modelBuilder.Entity<Badge>()
            .HasKey(badge => badge.Id);
        modelBuilder.Entity<CanvasUser>()
            .HasKey(user => user.Id);
        modelBuilder.Entity<PostContent>()
            .HasKey(content => content.Id);
        modelBuilder.Entity<BannedContent>()
            .HasKey(bannedContent => bannedContent.Id);

        
        // Unique account properties
        modelBuilder.Entity<Account>()
            .HasIndex(account => account.Username)
            .IsUnique();
        modelBuilder.Entity<Account>()
            .HasIndex(account => account.Email)
            .IsUnique();
        // Unique reddit account bindings (multiple accounts can't bind to the same reddit user)
        //modelBuilder.Entity<AccountRedditAuth>()
        //    .HasIndex(verification => verification.RedditId)
        //    .IsUnique();
        // Unique authentication verification codes
        modelBuilder.Entity<AccountPendingVerification>()
            .HasIndex(verification => verification.Code)
            .IsUnique();
        // Unique instance vanity names
        modelBuilder.Entity<Instance>()
            .HasIndex(instance => instance.VanityName)
            .IsUnique();
        // Unique post content key
        modelBuilder.Entity<Post>()
            .HasIndex(post => post.ContentUploadKey)
            .IsUnique();
        // Unique canvas userIntId (multiple canvas users can't own a single instance account)
        modelBuilder.Entity<CanvasUser>()
            .HasIndex(user => user.UserIntId)
            .IsUnique();
        // Unique post content key
        modelBuilder.Entity<PostContent>()
            .HasIndex(content => content.ContentKey)
            .IsUnique();
        
        
        // Relationships
        // Badges
        modelBuilder.Entity<Account>()
            .HasMany(account => account.Badges)
            .WithOne(badge => badge.Owner)
            .HasForeignKey(badge => badge.OwnerId);
        // Reddit auth
        //modelBuilder.Entity<Account>()
        //    .HasOne(account => account.RedditAuth)
        //    .WithOne(redditAuth => redditAuth.Account)
        //    .HasForeignKey<AccountRedditAuth>(redditAuth => redditAuth.AccountId);
        // Post auth accounts
        modelBuilder.Entity<Account>()
            .HasMany(account => account.Posts)
            .WithOne(post => post.AccountAuthor)
            .HasForeignKey(post => post.AccountAuthorId);
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
        
        // Blocked content
        modelBuilder.Entity<BannedContent>()
            .HasOne(bannedContent => bannedContent.Moderator)
            .WithMany(moderator => moderator.BannedContents)
            .HasForeignKey(bannedContent => bannedContent.ModeratorId);
        
        // Instance
        modelBuilder.Entity<Instance>()
            .HasMany(instance => instance.Users)
            .WithOne(user => user.Instance)
            .HasForeignKey(user => user.InstanceId);
        
        // Post canvas user accounts
        modelBuilder.Entity<CanvasUser>()
            .HasMany(user => user.Posts)
            .WithOne(post => post.CanvasUserAuthor)
            .HasForeignKey(post => post.CanvasUserAuthorId);

        // Expiration date
        modelBuilder.Entity<AccountPendingVerification>()
            .Property(verification => verification.ExpirationDate)
            .IsRequired();
        
        // AccountRefreshToken configuration
        modelBuilder.Entity<AccountRefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Token)
                .IsRequired()
                .HasMaxLength(255);

            entity.HasOne(e => e.Account)
                .WithMany(a => a.RefreshTokens)
                .HasForeignKey(e => e.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // CanvasUserRefreshToken configuration
        modelBuilder.Entity<CanvasUserRefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Token)
                .IsRequired()
                .HasMaxLength(255);

            entity.HasOne(e => e.CanvasUser)
                .WithMany(c => c.RefreshTokens)
                .HasForeignKey(e => e.CanvasUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}