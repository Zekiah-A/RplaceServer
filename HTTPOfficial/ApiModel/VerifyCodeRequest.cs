namespace HTTPOfficial.ApiModel;

public class VerifyCodeRequest
{
    public string Code { get; set; }
    public int AccountId { get; set; }

    public VerifyCodeRequest(string code, int accountId)
    {
        Code = code;
        AccountId = accountId;
    }
}