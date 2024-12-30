using System.Net;

namespace AuthOfficial;

public record EmailAuthCompletion(string AuthCode, IPAddress Address, DateTime StartDate);