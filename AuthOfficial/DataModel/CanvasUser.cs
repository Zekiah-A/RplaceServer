using System.Text.Json.Serialization;
using AuthOfficial.Authorization;

namespace AuthOfficial.DataModel;

// Links a canvas user to an account
public class CanvasUser
{
    public int Id { get; set; }
    public int UserIntId { get; set; }

    public string SecurityStamp { get; set; } = null!;

    // Navigation property to user posts
    [JsonIgnore]
    public List<Post> Posts { get; set; } = [];

    public int InstanceId { get; set; }
    // Navigation property to parent instance
    [JsonIgnore]
    public Instance Instance { get; set; } = null!;

    public int? AccountId { get; set; }
    // Navigation property to linked account
    [JsonIgnore]
    public Account? Account { get; set; } = null!;
    // Navigation property to refresh tokens
    [JsonIgnore]
    public List<CanvasUserRefreshToken> RefreshTokens { get; set; } = new();
}