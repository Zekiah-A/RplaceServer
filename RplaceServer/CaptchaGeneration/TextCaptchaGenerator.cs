using SkiaSharp;

namespace RplaceServer.CaptchaGeneration;

public class TextCaptchaGenerator : ICaptchaGenerator
{
    private static readonly Random Random = new Random();
    private static readonly string FontFile = "NotoColorEmoji-Regular.ttf";

    private readonly string[] Strings =
    {
        "rplace", "blobkat", "zekiahepic", "pixels", "game", "donate", "flag", "art", "build", "team", "create", "open",
        "canvas", "board", "anarchy", "reddit", "blank", "colour", "play", "teams", "war", "raid", "make", "learn", "fun"
    };
    private static SKPaint Font;

    static TextCaptchaGenerator()
    {
        // TODO: Figure out how to resolve font file using GameData
        //SKTypeface.FromFile(Path.Join(gameData.SaveDataFolder, "CaptchaGeneration", "NotoColorEmoji-Regular.ttf")),
        Font = new SKPaint
        {
            Typeface = SKTypeface.FromFile(FontFile),
            TextSize = 32
        };
    }

    public CaptchaGenerationResult Generate()
    {
        var dummies = new string[10];
        for (var i = 0; i < 10; i++)
        {
            dummies[i] = Strings[Random.Next(0, Strings.Length - 1)];
        }
        var answer = dummies[Random.Next(0, 9)];
        
        var bitmap = new SKBitmap(64, 64);
        var canvas = new SKCanvas(bitmap);
        var background = new SKPaint
        {
            Color = new SKColor((byte) Random.Next(), (byte) Random.Next(), (byte) Random.Next())
        };
        
        canvas.DrawRect(0, 0, 64, 64, background);
        canvas.DrawText(answer, 32, 32, Font);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Webp, 80);
        var stream = new MemoryStream();
        data.SaveTo(stream);
        stream.Flush();

        var responseDummies = string.Join("", dummies);
        var generationResponse = new CaptchaGenerationResult(answer, responseDummies, stream.ToArray());
        return generationResponse;
    }
}