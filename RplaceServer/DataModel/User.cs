namespace RplaceServer.DataModel;

public class User
{
    public int Id { get; set; }
    
    public int? AccountId { get; set; }
    
    public string? ChatName { get; set; }
    public string Token { get; set; }
    public int PixelsPlaced { get; set; }
    public int PlayTimeSeconds { get; set; }
}
