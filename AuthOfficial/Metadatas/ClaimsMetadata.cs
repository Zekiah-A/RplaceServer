namespace AuthOfficial.Metadatas;

public class ClaimsMetadata
{
    public string[] Types { get; }

    public ClaimsMetadata(string[] types)
    {
        Types = types;
    }
}