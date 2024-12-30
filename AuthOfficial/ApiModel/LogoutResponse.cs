namespace AuthOfficial.ApiModel;

public record LogoutResponse
{
    public int AccountId { get; }

    public LogoutResponse(int accountId)
    {
        AccountId = accountId;
    }
}
