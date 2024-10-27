namespace RplaceServer.DataModel;

public class LiveChatReport
{
    public int Id { get; set; }
    
    public int ReporterId { get; set; }
    // Navigation property to user who made the report
    public User? Reporter { get; set; }
    
    public int MessageId { get; set; }
    public string Reason { get; set; }
    public DateTime Date { get; set; }
}