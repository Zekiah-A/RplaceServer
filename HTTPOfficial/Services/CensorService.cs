using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using CoenM.ImageHash;
using CoenM.ImageHash.HashAlgorithms;
using HTTPOfficial.Configuration;
using HTTPOfficial.DataModel;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace HTTPOfficial.Services;

public partial class CensorService
{
    private readonly IOptionsMonitor<CensorConfiguration> config;
    private readonly DatabaseContext database;
    private readonly ILogger logger;
    
    private readonly HttpClient httpClient;
    private readonly JsonSerializerOptions jsonSerializerOptions;
    
    [GeneratedRegex(@"https?:\/\/(\w+\.)+\w{2,15}(\/\S*)?|(\w+\.)+\w{2,15}\/\S*|(\w+\.)+(tk|ga|gg|gq|cf|ml|fun|xxx|webcam|sexy?|tube|cam|p[o]rn|adult|com|net|org|online|ru|co|info|link)")]
    private static partial Regex BannedUrlsRegex();

    public CensorService(IOptionsMonitor<CensorConfiguration> config, DatabaseContext database, ILogger<CensorService> logger)
    {
        this.config = config;
        this.database = database;
        this.logger = logger;

        httpClient = new HttpClient();
        jsonSerializerOptions = new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }
    
    public bool IsContentBanned(Stream data, string contentType)
    {
        const string logPrefix = "Could not check if content is banned:";
        
        switch (contentType)
        {
            case "image/gif":
            {
                using (var gifImage = Image.Load<Rgba32>(data))
                {
                    var frameCount = gifImage.Frames.Count;
                    var maxFramesToProcess = Math.Min(config.CurrentValue.DefaultProcessMaxGifFrames, frameCount);
                    var frameInterval = frameCount / maxFramesToProcess;
                    var perceptualHash = new PerceptualHash();

                    for (int i = 0; i < frameCount; i += frameInterval)
                    {
                        var frame = gifImage.Frames.CloneFrame(i);
                        var frameHash = perceptualHash.Hash(frame);
                        if (IsHashBanned(frameHash, logPrefix))
                        {
                            return true;
                        }
                    }
                }
                break;
            }
            case "image/jpeg" or "image/png" or "image/webp":
            {
                var perceptualHash = new PerceptualHash();
                var contentHash = perceptualHash.Hash(data);
                if (IsHashBanned(contentHash, logPrefix))
                {
                    return true;
                }
                break;
            }
            default:
            {
                logger.LogError("{logPrefix} Content of type {contentType} not recognised", logPrefix, contentType);
                return false;
            }
        }

        return false;
    }

    private bool IsHashBanned(ulong contentHash, string logPrefix = "Could not check if content's hash is banned:")
    {
        var applicableBannedContents = database.BlockedContents.Where(content =>
            content.HashType == ContentHashType.Perceptual && content.FileType == ContentFileType.Image);
        foreach (var bannedContent in applicableBannedContents)
        {
            if (ulong.TryParse(bannedContent.Hash, out var bannedHash) && 
                CompareHash.Similarity(contentHash, bannedHash) > config.CurrentValue.DefaultFilterMinPerceptualPercent)
            {
                return true;
            }
            logger.LogWarning("{logPrefix} Content's {id}'s hash could not be parsed as ulong",
                logPrefix, bannedContent.Id);
        }
        return false;
    }

    public async Task<bool> ProbablyHasProfanity(string text)
    {
        const string logPrefix = "Failed to query profanity API:";
        try
        {
            var content = JsonContent.Create(new { Message = text }, options: jsonSerializerOptions);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            var result = await httpClient.PostAsync("https://vector.profanity.dev", content);
            if (!result.IsSuccessStatusCode)
            {
                logger.LogError("{logPrefix} Status {statusCode} received", logPrefix, result.StatusCode);
                return false;
            }

            var profanityResponse = await result.Content.ReadFromJsonAsync<ProfanityResponse>();
            if (profanityResponse is null)
            {
                logger.LogError("{logPrefix} Deserialised profanity response {statusCode} received", logPrefix, result.StatusCode);
                return false;
            }
            return profanityResponse.IsProfanity;
        }
        catch (Exception error)
        {
            logger.LogError("{logPrefix} {error}", logPrefix, error);
            return false;
        }
    }

    public string CensorBanned(string text)
    {
        return CensorBannedWords(CensorBannedUrls(text));
    }

    public string CensorBannedUrls(string text)
    {
        return BannedUrlsRegex().Replace(text, match => 
            {
                var url = match.Value.Replace("https://", "").Replace("http://", "").Split('/')[0];
                return config.CurrentValue.DefaultFilterAlllowedDomains.Contains(url) ? match.Value : new string('*', match.Length);
            })
            .Trim();
    }

    public string CensorBannedWords(string text)
    {
        if (string.IsNullOrEmpty(text) || config.CurrentValue.DefaultFilterBannedWords.Count == 0)
        {
            return text;
        }

        var pattern = @"\b(" + string.Join("|", config.CurrentValue.DefaultFilterBannedWords.Select(Regex.Escape)) + @")\b";
        var regex = new Regex(pattern, RegexOptions.IgnoreCase);

        return regex.Replace(text, match => new string('*', match.Value.Length));
    }
}