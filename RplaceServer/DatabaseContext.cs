using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using RplaceServer.DataModel;

namespace RplaceServer;

public class DatabaseContext : DbContext
{
    public string Path { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<LiveChatMessage> LiveChatMessages { get; set; }
    public DbSet<LiveChatReaction> LiveChatReactions { get; set; }
    public DbSet<PlaceChatMessage> PlaceChatMessages { get; set; }
    public DbSet<Ban> Bans { get; set; }
    public DbSet<Mute> Mutes { get; set; }
    public DbSet<Session> Sessions { get; set; }
    public DbSet<UserVip> UserVips { get; set; }
    public DbSet<LiveChatDeletion> LiveChatDeletions { get; set; }
    public DbSet<LiveChatReport> LiveChatReports { get; set; }

    public DatabaseContext(DbContextOptions<DatabaseContext> options) : base(options)
    {
        Path = ":memory:";
    }

    public DatabaseContext()
    {
        Path = ":memory:";
    }
    
    public DatabaseContext(string path)
    {
        Path = path;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite($"Data Source={Path}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.HasIndex(u => u.AccountId).IsUnique(false);
        });

        modelBuilder.Entity<LiveChatMessage>(entity =>
        {
            entity.HasKey(message => message.Id);
            entity.HasOne(message => message.Sender)
                .WithMany()
                .HasForeignKey(message => message.SenderId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(message => message.RepliesTo)
                .WithMany()
                .HasForeignKey(message => message.RepliesToId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(message => message.Deletion)
                .WithMany()
                .HasForeignKey(message => message.DeletionId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LiveChatReaction>(entity =>
        {
            entity.HasKey(reaction => reaction.Id);
            entity.HasOne(reaction => reaction.Message)
                .WithMany()
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(reaction => reaction.Sender)
                .WithMany()
                .HasForeignKey(reaction => reaction.SenderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PlaceChatMessage>(entity =>
        {
            entity.HasKey(message => message.Id);
            entity.HasOne(message => message.Sender)
                .WithMany()
                .HasForeignKey(message => message.SenderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Ban>(entity =>
        {
            entity.HasKey(ban => ban.Id);
            entity.HasOne(ban => ban.User)
                .WithMany()
                .HasForeignKey(ban => ban.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(ban => ban.Muter)
                .WithMany()
                .HasForeignKey(ban => ban.MuterId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Mute>(entity =>
        {
            entity.HasKey(mute => mute.Id);
            entity.HasOne(mute => mute.User)
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(mute => mute.Moderator)
                .WithMany()
                .HasForeignKey(mute => mute.MuterId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Session>(entity =>
        {
            entity.HasKey(session => session.Id);
            entity.HasOne(session => session.User)
                .WithMany()
                .HasForeignKey(session => session.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserVip>(entity =>
        {
            entity.HasKey(vip => vip.UserId);
            entity.HasOne(vip => vip.User)
                .WithMany()
                .HasForeignKey(vip => vip.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LiveChatDeletion>(entity =>
        {
            entity.HasKey(deletion => deletion.Id);
            entity.HasOne(deletion => deletion.Deleter)
                .WithMany()
                .HasForeignKey(deletion => deletion.DeleterId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LiveChatReport>(entity =>
        {
            entity.HasKey(report => report.Id);
            entity.HasOne(report => report.Reporter)
                .WithMany()
                .HasForeignKey(report => report.ReporterId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(report => report.Message)
                .WithMany()
                .HasForeignKey(report => report.MessageId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}