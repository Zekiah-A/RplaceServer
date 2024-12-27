namespace HTTPOfficial.ApiModel;

public class LoginDetailsResponse
{
    public int AccountId { get; set; }
    public string Username { get; set; }
    public string Email { get; set; }
    public bool EmailVerified { get; set; }

    public LoginDetailsResponse(int accountId, string username, string email, bool emailVerified = false)
    {
        AccountId = accountId;
        Username = username;
        Email = email;
        EmailVerified = emailVerified;
    }
}