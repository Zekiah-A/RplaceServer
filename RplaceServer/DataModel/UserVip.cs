namespace RplaceServer.DataModel;

public class UserVip
{
    public int UserId { get; set; }
    // Navigation property to VIP user
    public User? User { get; set; }
    
    public string KeyHash { get; set; }
    public DateTime LastUsed { get; set; }
}