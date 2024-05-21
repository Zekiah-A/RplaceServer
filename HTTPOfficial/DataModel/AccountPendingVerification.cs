using System.Text.Json.Serialization;

namespace HTTPOfficial.DataModel;

public class AccountPendingVerification
{
    public int Id;
    public bool Initial { get; set; }
    public string Code { get; set; } = null!;
    public DateTime CreationDate { get; set; }
    
    public int AccountId { get; set; }
    // Navigation property to account instance
    [JsonIgnore]
    public Account Account { get; set; } = null!;

    public AccountPendingVerification() { }

    public AccountPendingVerification(int accountId, string code, DateTime creationDate)
    {
        AccountId = accountId;
        Code = code;
        CreationDate = creationDate;
        Initial = false;
    }
}