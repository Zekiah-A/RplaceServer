using System.Text.Json.Serialization;

namespace AuthOfficial.DataModel;

public class AccountRefreshToken
{
    public int Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; }
    public DateTime ExpirationDate { get; set; }
    public DateTime? RevokationDate { get; set; }

    public int AccountId { get; set; }
    // Navigation property to account
    [JsonIgnore]
    public Account Account { get; set; } = null!;
}