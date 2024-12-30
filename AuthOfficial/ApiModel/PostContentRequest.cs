namespace AuthOfficial.ApiModel;

public record PostContentRequest(string ContentUploadKey, IFormFile File);