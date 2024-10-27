namespace RplaceServer.DataModel;

public class PlaceChatMessage
{
    public int Id { get; set; }
    
    public DateTime SendDate { get; set; }
    public string Message { get; set; }
    
    public int SenderId { get; set; }
    // Navigation property to place chat message sender
    public User Sender { get; set; }
    
    public int X { get; set; }
    public int Y { get; set; }
}