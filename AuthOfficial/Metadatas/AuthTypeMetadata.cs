namespace AuthOfficial.Metadatas;

internal class AuthTypeMetadata
{
    public AuthType AuthTypeFlags { get; }

    public AuthTypeMetadata(AuthType authTypeFlags)
    {
        AuthTypeFlags = authTypeFlags;
    }
}