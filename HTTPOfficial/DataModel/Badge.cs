using System.Text.Json.Serialization;

namespace HTTPOfficial.DataModel;

public class Badge
{
    public int Id { get; set; }
    public BadgeType Type { get; set; }
    public DateTime AwardDate { get; set; }

    public int OwnerId { get; set; }
    // Navigation property to account owner
    [JsonIgnore]
    public Account Owner { get; set; } = null!;

    public Badge() { }

    public Badge(BadgeType type, DateTime awardDate)
    {
        Type = type;
        AwardDate = awardDate;
    }
}