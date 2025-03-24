namespace AuthOfficial;

public class JwtLinkedUser
{
    public int Id { get; set; }
    public int InstanceId { get; set; }
    public int UserIntId { get; set; }

    public JwtLinkedUser(int userId, int instanceId, int userIntId)
    {
        Id = userId;
        InstanceId = instanceId;
        UserIntId = userIntId;
    }
}