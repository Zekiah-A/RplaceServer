namespace RplaceServer.DataModel;

public class LiveChatDeletion
{
    public int Id { get; set; }
    
    public int DeleterId { get; set; }
    // Navigation property to user who deleted the message
    public User Deleter { get; set; }
    
    public string? Reason { get; set; }
    public DateTime Date { get; set; }
}