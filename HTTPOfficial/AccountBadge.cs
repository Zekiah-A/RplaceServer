namespace HTTPOfficial;

public class AccountBadge
{
    public int Id { get; set; }
    public Badge Type { get; set; }
    public DateTime AwardDate { get; set; }

    public int OwnerId { get; set; }
    // Navigation property to account owner
    public AccountData Owner { get; set; } = null!;

    public AccountBadge(Badge type, DateTime awardDate)
    {
        Type = type;
        AwardDate = awardDate;
    }
}