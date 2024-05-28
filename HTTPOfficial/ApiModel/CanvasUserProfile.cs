namespace HTTPOfficial.ApiModel;

public record CanvasUserResponse(int Id, int UserIntId, int InstanceId, int? AccountId,
    string ChatName, DateTime LastJoined, int PixelsPlaced, int PlayTimeSeconds);