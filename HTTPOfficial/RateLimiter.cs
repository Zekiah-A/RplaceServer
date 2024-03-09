namespace HTTPOfficial;

using System.Net;

public class RateLimiter
{
    private readonly Dictionary<IPAddress, DateTime> registeredIPs;
    private readonly TimeSpan limitPeriod;

    public RateLimiter(TimeSpan limit)
    {
        limitPeriod = limit;
        registeredIPs = new Dictionary<IPAddress, DateTime>();
    }

    public bool IsAuthorised(IPAddress address, bool extendIfNot = false)
    {
        if (!registeredIPs.ContainsKey(address) || !registeredIPs.TryGetValue(address, out var startDate))
        {
            registeredIPs.Add(address, DateTime.Now);
            return true;
        }

        foreach (var pair in registeredIPs)
        {
            if (pair.Key.Equals(address))
            {
                continue;
            }

            if (DateTime.Now - pair.Value > limitPeriod)
            {
                registeredIPs.Remove(pair.Key);
            }
        }

        if (DateTime.Now - startDate < limitPeriod)
        {
            if (extendIfNot)
            {
                registeredIPs[address] = DateTime.Now;
            }

            return false;
        }

        registeredIPs[address] = DateTime.Now;
        return true;
    }
}