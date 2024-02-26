namespace HTTPOfficial;

public class AccountBadge
{
    public int Id { get; set; }
    public Badge Badge { get; set; }
    public DateTime AwardDate { get; set; }

    public int OwnerId { get; set; }
    // Navigation property to account owner
    public AccountData Owner { get; set; } = null!;

    public AccountBadge(Badge badge, DateTime awardDate)
    {
        Badge = badge;
        AwardDate = awardDate;
    }
}