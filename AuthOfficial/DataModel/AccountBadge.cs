using System.Text.Json.Serialization;
using AuthOfficial.Authorization;

namespace AuthOfficial.DataModel;

public class AccountBadge

{
    public int Id { get; set; }
    public BadgeType Type { get; set; }
    public DateTime AwardDate { get; set; }

    public int OwnerId { get; set; }
    // Navigation property to account owner
    [JsonIgnore]
    public Account Owner { get; set; } = null!;

    public AccountBadge() { }

    public AccountBadge(BadgeType type, DateTime awardDate)
    {
        Type = type;
        AwardDate = awardDate;
    }
}