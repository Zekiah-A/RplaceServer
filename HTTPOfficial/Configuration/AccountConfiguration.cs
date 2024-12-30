using HTTPOfficial.DataModel;

namespace HTTPOfficial.Configuration;

public class AccountConfiguration
{
    public Dictionary<AccountTier, int> AccountTierInstanceLimits { get; init; }
    public int UnverifiedAccountExpiryMinutes = 15;
}
