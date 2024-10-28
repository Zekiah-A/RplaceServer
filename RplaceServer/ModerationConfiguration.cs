using System.Text.RegularExpressions;

namespace RplaceServer;

public interface IModerationConfiguration
{
    // Will enable cloudflare cf-clearance headers checks if present to allow only cloudflare validated clients
    public bool UseCloudflare { get; set; }
    public int ChatCooldownMs { get; set; }
    public bool CaptchaEnabled { get; set; }
    // By default will use regexes provided in censors.txt to censor chat messages for profanity
    public bool CensorChatMessages { get; set; }
    public List<Regex> ChatCensorRegexes { get; set; }
    public List<Regex> ChatAllowedDomainsRegexes { get; set; }
}

public class ConfigureModerationOptions : IModerationConfiguration
{
    public bool UseCloudflare { get; set; }
    public int ChatCooldownMs { get; set; }
    public bool CaptchaEnabled { get; set; }
    public bool CensorChatMessages { get; set; }
    public List<Regex> ChatCensorRegexes { get; set; } = [];
    public List<Regex> ChatAllowedDomainsRegexes { get; set; } = [];
}
