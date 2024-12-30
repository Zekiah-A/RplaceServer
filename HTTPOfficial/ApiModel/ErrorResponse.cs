namespace HTTPOfficial.ApiModel;

public class ErrorResponse
{
    public string Message { get; init; }
    public string Key { get; init; }
    public object Metadata { get; init; }

    public ErrorResponse(string message, string key, object? metadata = null)
    {
        Message = message;
        Key = key;
        Metadata = metadata;
    }
}