namespace HTTPOfficial.DataModel;

[Flags]
public enum AccountTier
{
    Free = 0,
    Bronze = 1,
    Silver = 2,
    Gold = 4,
    Moderator = 8,
    Administrator = 16
}