namespace HTTPOfficial.ApiModel;

public record PostUploadRequest(
    LinkageSubmission? CanvasUser,
    int? AccountId,
    string Title,
    string Description);