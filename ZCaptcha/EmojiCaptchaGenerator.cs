using SkiaSharp;

namespace ZCaptcha;

public class EmojiCaptchaGenerator : ICaptchaGenerator
{
    private static int entropy = 0;
    private static readonly Random Random = new Random();
    private static readonly string[] Emojis =
    {
        "😎", "🤖", "🔥", "🏠", "🤡", "👋", "💩", "⚽", "👅", "🧠", "🕶", "🌳", "🌍", "🌈", "🎅", "👶", "👼",
        "🥖", "🍆", "🎮", "🎳", "🗿", "📱", "🔑", "👺", "🤯", "🤬", "🦩", "🍔", "🎬", "🚨", "⚡️", "🍪", "🕋", "🎉",
        "📋", "🚦", "🔇", "🥶", "💼", "🎩", "🎒", "🦅", "🧊", "★", "✅", "😂", "😍", "🚀", "😈", "👟", "🍷", "🚜",
        "🐥", "🔍", "🎹", "🚻", "🚗", "🏁", "🥚", "🔪", "🍕", "🐑", "🖱", "😷", "🌱", "🏀", "🛠", "🤮", "💂", "📎",
        "🎄", "🕯️", "🔔", "⛪", "☃", "🍷", "❄", "🎁", "🩸"
    };
    
    private static readonly int DummiesCount = 10;
    private static readonly int Width = 96;
    private static readonly int Height = 96;
    private static readonly int FontSize = 48;
    private static readonly int Noise1Size = 3;
    private static readonly float Noise1Opacity = 0.35f;
    private static readonly float TextShift = 32;
    private static readonly float TextRotateRad = 0.4f;
    private readonly SKTypeface captchaFont;
    private readonly SKBitmap bitmap;
    private readonly SKCanvas canvas;

    public EmojiCaptchaGenerator(string fontPath)
    {
        captchaFont = SKTypeface.FromFile(fontPath);
        bitmap = new SKBitmap(Width, Height);
        canvas = new SKCanvas(bitmap);
    }
    
    private static SKColor RandomColor()
    {
        byte[] colorBytes = new byte[3];
        Random.NextBytes(colorBytes);
        return new SKColor(colorBytes[0], colorBytes[1], colorBytes[2]);
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

        using (var paint = new SKPaint())
        {
            paint.IsAntialias = true;

            // Draw noise
            for (float x = 0; x < Width / Noise1Size; x++)
            {
                for (float y = 0; y < Height / Noise1Size; y++)
                {
                    paint.Color = RandomColor();
                    canvas.DrawRect(x * Noise1Size, y * Noise1Size, Noise1Size, Noise1Size, paint);
                }
            }

            // Draw text
            var textX = Width / 2 + (float)(Random.NextDouble() * TextShift - (TextShift / 2));
            var textY = Height / 2 + (float)(Random.NextDouble() * TextShift - (TextShift / 2));
            var textR = (float)(Random.NextDouble() * TextRotateRad - TextRotateRad / 2);

            paint.TextSize = FontSize;
            paint.Color = SKColors.Black;
            paint.IsStroke = false;
            paint.Typeface = captchaFont;

            using (var autoRestore = new SKAutoCanvasRestore(canvas, true))
            {
                canvas.Translate(Width / 2, Height / 2);
                canvas.RotateDegrees(textR);
                canvas.Translate(-Width / 2, -Height / 2);
                canvas.Translate(textX, textY);
                canvas.DrawText(answer, 0, 0, paint);
            }

            // Draw lines
            for (var i = 0; i < 8; i++)
            {
                paint.Color = RandomColor();
                paint.IsStroke = true;
                paint.StrokeWidth = 1;

                var startX = (float)(Random.NextDouble() * Width);
                var startY = (float)(Random.NextDouble() * Height);
                var endX = (float)(Random.NextDouble() * Width);
                var endY = (float)(Random.NextDouble() * Height);

                canvas.DrawLine(startX, startY, endX, endY, paint);
            }
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
