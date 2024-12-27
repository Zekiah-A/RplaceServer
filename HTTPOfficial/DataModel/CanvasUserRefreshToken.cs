using System.Text.Json.Serialization;

namespace HTTPOfficial.DataModel;

public class CanvasUserRefreshToken
{
    public int Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; }
    public DateTime ExpirationDate { get; set; }
    public DateTime? RevokationDate { get; set; }

    public int CanvasUserId { get; set; }
    // Navigation property to canvas user
    [JsonIgnore]
    public CanvasUser CanvasUser { get; set; } = null!;
}