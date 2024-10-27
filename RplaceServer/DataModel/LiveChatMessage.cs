namespace RplaceServer.DataModel;

public class LiveChatMessage
{
    public int Id { get; set; }
    
    public DateTime SendDate { get; set; }
    public string Channel { get; set; }
    public string Message { get; set; }
    
    public int SenderId { get; set; }
    // Navigation property to message sender
    public User Sender { get; set; }
    
    public int? RepliesToId { get; set; }
    // Navigation property to message being replied to
    public LiveChatMessage? RepliesTo { get; set; }
    
    public int? DeletionId { get; set; }
    // Navigation property to message deletion notice
    public LiveChatDeletion? Deletion { get; set; }
}