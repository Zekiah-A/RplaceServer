namespace HTTPOfficial.ApiModel;

public class LoginDetailsResponse
{
    public int Id { get; set; }
    public string? Token { get; set; }
    public string? RefreshToken { get; set; }
    public string Username { get; set; }
    public string Email { get; set; }
    public bool EmailVerified { get; set; }

    public LoginDetailsResponse(int id, string? token, string? refreshToken, string username, string email, bool emailVerified = false)
    {
        Id = id;
        Token = token;
        RefreshToken = refreshToken;
        Username = username;
        Email = email;
        EmailVerified = emailVerified;
    }
}