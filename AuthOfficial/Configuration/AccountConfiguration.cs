using AuthOfficial.DataModel;

namespace AuthOfficial.Configuration;

public class AccountConfiguration
{
    public Dictionary<AccountTier, int> AccountTierInstanceLimits { get; init; }
    public int UnverifiedAccountExpiryMinutes = 15;
}
