namespace RplaceServer.DataModel;

public class Mute
{
    public int Id { get; set; }
    
    public DateTime StartDate { get; set; }
    public DateTime FinishDate { get; set; }
    
    public int UserId { get; set; }
    // Navigation property to user who is muted
    public User User { get; set; }
    
    public int MuterId { get; set; }
    // Navigation property to moderator who performed the mute
    public User Moderator { get; set; }
    
    public string Reason { get; set; }
}