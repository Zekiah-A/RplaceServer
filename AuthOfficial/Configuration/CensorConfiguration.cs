namespace AuthOfficial.Configuration;

public class CensorConfiguration
{
    public List<string> DefaultFilterAlllowedDomains { get; init; }
    public List<string> DefaultFilterBannedWords { get; init; }
    public int DefaultFilterMinPerceptualPercent { get; set; }
    public int DefaultProcessMaxGifFrames { get; set; }
}