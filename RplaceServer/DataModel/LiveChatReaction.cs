namespace RplaceServer.DataModel;

public class LiveChatReaction
{
    public int Id { get; set; }
    
    // Navigation property to message of reaction
    public LiveChatMessage Message { get; set; }
    
    public string Reaction { get; set; }
    
    public int SenderId { get; set; }
    // Navigation property to message reaction sender
    public User Sender { get; set; }
}