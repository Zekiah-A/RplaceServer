namespace AuthOfficial.Metadatas;

internal class AuthTypeMetadata
{
    public AuthTypeFlags AuthTypeFlags { get; }

    public AuthTypeMetadata(AuthTypeFlags authTypeFlags)
    {
        AuthTypeFlags = authTypeFlags;
    }
}