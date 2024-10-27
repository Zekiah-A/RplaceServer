namespace RplaceServer.DataModel;

public class Session
{
    public int Id { get; set; }
    
    public string Ip { get; set; }
    public string UserAgent { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime FinishDate { get; set; }
}