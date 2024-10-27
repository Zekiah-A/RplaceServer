namespace RplaceServer.DataModel;

public class Ban
{
    public int Id { get; set; }
    
    public DateTime StartDate { get; set; }
    public DateTime FinishDate { get; set; }
    
    public int UserId { get; set; }
    // Navigation property to user who was banned 
    public User User { get; set; }
    
    public int MuterId { get; set; }
    // Navigation property to moderator who performed the ban
    public User Muter { get; set; }
    
    public string Reason { get; set; }
}