using System.Net;

namespace PlaceHttpsServer;

public class RateLimiter
{
    private readonly Dictionary<IPAddress, DateTime> RegisteredIPs;
    private readonly TimeSpan LimitPeriod;
    
    public RateLimiter(TimeSpan limitPeriod)
    {
        LimitPeriod = limitPeriod;
        RegisteredIPs = new Dictionary<IPAddress, DateTime>();
    }

    public bool IsAuthorised(IPAddress address, bool extendIfNot = false)
    {
        if (!RegisteredIPs.ContainsKey(address) || !RegisteredIPs.TryGetValue(address, out var startDate))
        {
            RegisteredIPs.Add(address, DateTime.Now);
            return false;
        }

        if (DateTime.Now - startDate < LimitPeriod)
        {
            if (extendIfNot)
            {
                RegisteredIPs[address] = DateTime.Now;
            }
            
            return false;
        }

        RegisteredIPs.Remove(address);
        return true;
    }
}