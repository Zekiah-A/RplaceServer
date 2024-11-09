using SkiaSharp;

namespace ZCaptcha;

public class EmojiCaptchaGenerator : ICaptchaGenerator
{
    private static int entropy = 0;
    private static readonly Random Random = new();
    private static readonly string[] Emojis =
    {
        "😎", "🤖", "🔥", "🏠", "🤡", "👋", "💩", "⚽", "👅", "🧠", "🕶", "🌳", "🌍", "🌈", "🎅", "👶", "👼",
        "🥖", "🍆", "🎮", "🎳", "🗿", "📱", "🔑", "👺", "🤯", "🤬", "🦩", "🍔", "🎬", "🚨", "⚡️", "🍪", "🕋", "🎉",
        "📋", "🚦", "🔇", "🥶", "💼", "🎩", "🎒", "🦅", "🧊", "★", "✅", "😂", "😍", "🚀", "😈", "👟", "🍷", "🚜",
        "🐥", "🔍", "🎹", "🚻", "🚗", "🏁", "🥚", "🔪", "🍕", "🐑", "🖱", "😷", "🌱", "🏀", "🛠", "🤮", "💂", "📎",
        "🎄", "🕯️", "🔔", "⛪", "☃", "🍷", "❄", "🎁", "🩸"
    };

    private const int DummiesCount = 10;
    private const int Width = 96;
    private const int Height = 96;
    private const int FontSize = 48;
    private const float Noise1Size = 3.0f;
    private const byte Noise1Alpha = 90;
    private const float TextShift = 32;
    private const float TextRotateRad = 0.4f;
    private readonly SKTypeface captchaFont;
    private readonly SKBitmap bitmap;
    private readonly SKCanvas canvas;

    public EmojiCaptchaGenerator(string fontPath)
    {
        captchaFont = SKTypeface.FromFile(fontPath);
        bitmap = new SKBitmap(Width, Height);
        canvas = new SKCanvas(bitmap);
    }
    
    private static SKColor RandomColour(byte alpha = 255)
    {
        var colourBytes = new byte[3];
        Random.NextBytes(colourBytes);
        return new SKColor(colourBytes[0], colourBytes[1], colourBytes[2], alpha);
    }

    // Mirrors genEmojiCaptcha2 https://github.com/Zekiah-A/rslashplace2.github.io/blob/main/zcaptcha/server.js
    public CaptchaGenerationResult Generate()
    {
        var dummies = new List<string>();
        for (var i = 0; i < DummiesCount; i++)
        {
            string chosen;
            do
            {
                chosen = Emojis[Random.Next(Emojis.Length)];
            } while (dummies.Contains(chosen));
            dummies.Add(chosen);
        }

        var answer = dummies[Random.Next(dummies.Count)];
        entropy++;

        if (entropy % 10 == 0)
        {
            canvas.Clear(SKColors.Transparent);
        }

        using var paint = new SKPaint();
        paint.IsAntialias = true;
        paint.Color = SKColors.Black;
        paint.IsStroke = false;

        // Draw noise
        for (float x = 0; x < Width / Noise1Size; x++)
        {
            for (float y = 0; y < Height / Noise1Size; y++)
            {
                paint.Color = RandomColour(Noise1Alpha);
                canvas.DrawRect(x * Noise1Size, y * Noise1Size, Noise1Size, Noise1Size, paint);
            }
        }

        // Draw text
        var textX = Width / 2.0f + (float)(Random.NextDouble() * TextShift - (TextShift / 2));
        var textY = Height / 2.0f + (float)(Random.NextDouble() * TextShift - (TextShift / 2));
        var textR = (float)(Random.NextDouble() * TextRotateRad - TextRotateRad / 2);

        using var font = new SKFont();
        font.Size = FontSize;
        font.Typeface = captchaFont;

        using (new SKAutoCanvasRestore(canvas, true))
        {
            canvas.Translate(Width / 2.0f, Height / 2.0f);
            canvas.RotateDegrees(textR);
            canvas.Translate(-Width / 2.0f, -Height / 2.0f);
            canvas.Translate(textX, textY);
            canvas.DrawText(answer, 0, 0, SKTextAlign.Center, font, paint);
        }

        // Draw lines
        for (var i = 0; i < 8; i++)
        {
            paint.Color = RandomColour();
            paint.IsStroke = true;
            paint.StrokeWidth = 1;

            var startX = (float)(Random.NextDouble() * Width);
            var startY = (float)(Random.NextDouble() * Height);
            var endX = (float)(Random.NextDouble() * Width);
            var endY = (float)(Random.NextDouble() * Height);

            canvas.DrawLine(startX, startY, endX, endY, paint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 80);
        var stream = new MemoryStream();
        data.SaveTo(stream);
        stream.Flush();

        var responseDummies = string.Join('\n', dummies);
        var generationResponse = new CaptchaGenerationResult(answer, responseDummies, stream.ToArray());
        return generationResponse;
    }
}
