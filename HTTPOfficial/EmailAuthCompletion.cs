using System.Net;

namespace HTTPOfficial;

public record EmailAuthCompletion(string AuthCode, IPAddress Address, DateTime StartDate);