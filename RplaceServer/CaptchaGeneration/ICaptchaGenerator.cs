namespace RplaceServer.CaptchaGeneration;

public interface ICaptchaGenerator
{
    public CaptchaGenerationResult Generate();
}