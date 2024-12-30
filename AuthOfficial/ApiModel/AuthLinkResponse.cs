namespace AuthOfficial.ApiModel;

public class AuthLinkResponse
{
    public int Id { get; set; }
    public string Token { get; set; }
    public string RefreshToken { get; set; }
    public int UserIntId { get; set; }
    public int InstanceId { get; set; }

    public AuthLinkResponse(int id, string token, string refreshToken, int userIntId, int instanceId)
    {
        Id = id;
        Token = token;
        RefreshToken = refreshToken;
        UserIntId = userIntId;
        InstanceId = instanceId;
    }
}