namespace AuthOfficial.Metadatas;

internal class RateLimitMetadata
{
    public TimeSpan TimeSpan { get; }

    public RateLimitMetadata(TimeSpan timeSpan)
    {
        TimeSpan = timeSpan;
    }
}