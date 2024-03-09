namespace HTTPOfficial.ApiModel;

public record PostUploadRequest(string? Username, int? AccountId, string Title, string Description);