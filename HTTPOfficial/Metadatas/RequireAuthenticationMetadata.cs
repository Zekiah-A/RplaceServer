namespace HTTPOfficial.Metadatas;

internal class RequireAuthenticationMetadata
{
    public AuthenticationTypeFlags AuthTypeFlags { get;}

    public RequireAuthenticationMetadata(AuthenticationTypeFlags authTypeFlags)
    {
        AuthTypeFlags = authTypeFlags;
    }
}