namespace HTTPOfficial.ApiModel;

public record PostContentRequest(string ContentUploadKey, IFormFile File);