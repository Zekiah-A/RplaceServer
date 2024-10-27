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
        
    }
}