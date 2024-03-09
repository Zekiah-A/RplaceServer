using SkiaSharp;

namespace ZCaptcha;

public class TextCaptchaGenerator : ICaptchaGenerator
{
    private static readonly Random Random = new Random();
    private const int Width = 256;
    private const int Height = 256;
    private const int DummiesCount = 10;

    private readonly string[] Strings =
    {
        "rplace", "blobkat", "zekiahepic", "pixels", "game", "donate", "flag", "art", "build", "team", "create", "open",
        "canvas", "board", "anarchy", "reddit", "blank", "colour", "play", "teams", "war", "raid", "make", "learn", "fun",
        "lamda", "openmc", "count", "void", "community", "faction", "event", "live", "pixelart", "collab", "painting", "draw"
    };
    private static SKPaint Font;

    public TextCaptchaGenerator(string fontPath)
    {
        Font = new SKPaint
        {
            Typeface = SKTypeface.FromFile(fontPath),
            TextSize = 24
        };
    }

    public CaptchaGenerationResult Generate()
    {
        var dummies = new List<string>();
        for (var i = 0; i < DummiesCount; i++)
        {
            string chosen;
            do
            {
                chosen = Strings[Random.Next(Strings.Length)];
            } while (dummies.Contains(chosen));
            dummies.Add(chosen);
        }
        var answer = dummies[Random.Next(0, 9)];
        
        var bitmap = new SKBitmap(Width, Height);
        var canvas = new SKCanvas(bitmap);
        var background = new SKPaint
        {
            Color = new SKColor((byte) Random.Next(), (byte) Random.Next(), (byte) Random.Next())
        };
        
        canvas.DrawRect(0, 0, Width, Height, background);
        Font.TextSize = 1;
        var textWidth = Font.MeasureText(answer);
        Font.TextSize = Width / textWidth;
        var textX = Random.Next(-10, 10);
        var textY = Height / 2 - Font.TextSize / 2;
        var textR = Random.Next(-10, 10);
        using (var autoRestore = new SKAutoCanvasRestore(canvas, true))
        {
            canvas.Translate(Width / 2, Height / 2);
            canvas.RotateDegrees(textR);
            canvas.Translate(-Width / 2, -Height / 2);
            canvas.Translate(textX, textY);
            canvas.DrawText(answer, 0, 0, Font);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Webp, 80);
        var stream = new MemoryStream();
        data.SaveTo(stream);
        stream.Flush();

        var responseDummies = string.Join('\n', dummies);
        var generationResponse = new CaptchaGenerationResult(answer, responseDummies, stream.ToArray());
        return generationResponse;
    }
}
