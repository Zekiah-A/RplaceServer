namespace RplaceServer.CaptchaGeneration;

public record struct CaptchaGenerationResult(string Answer, string Dummies, byte[] ImageData);