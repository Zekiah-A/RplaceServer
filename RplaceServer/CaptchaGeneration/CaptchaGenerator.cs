using RplaceServer.Types;
using SkiaSharp;

namespace RplaceServer.CaptchaGeneration;

internal static class CaptchaGenerator
{
    private static Random random = new();

    private static readonly string[] Emojis =
    {
        "😎", "🤖", "🗣", "🔥", "🏠", "🤡", "👾", "👋", "💩", "⚽", "👅", "🧠", "🕶", "🌳", "🌍", "🌈", "🎅", "👶", "👼",
        "🥖", "🍆", "🎮", "🎳", "🚢", "🗿", "ඞ", "📱", "🔑", "❤", "👺", "🤯", "🤬", "🦩", "🍔", "🎬", "🚨", "⚡️", "🍪",
        "🕋", "🎉", "📋", "🚦", "🔇", "🥶", "💼", "🎩", "🎒", "🦅", "🧊", "★", "✅", "😂", "😍", "🚀", "😈", "👟", "🍷",
        "🚜", "🐥", "🔍", "🎹", "🚻", "🚗", "🏁", "🥚", "🔪", "🍕", "🐑", "🖱", "😷", "🌱", "🏀", "🛠", "🤮", "💂", "📎",
        "🎄", "🕯️", "🔔", "⛪", "☃", "🍷", "❄", "🎁", "🩸"
    };

    private static readonly string[] Strings =
    {
        "rplace", "blobkat", "zekiahepic", "pixels", "game", "donate", "flag", "art", "build", "team", "create", "open",
        "canvas", "board", "anarchy", "reddit", "blank", "colour", "play", "teams", "war", "raid", "make", "learn", "fun"
    };
    
    // TODO: There are currently skia-related crashes in the generator - fix.
    internal static (string Answer, string Dummies, byte[] ImageData) Generate(CaptchaType type)
    {
        var answer = "";
        var dummies = new string[10];
            
        switch (type)
        {
            case CaptchaType.Emoji:
                var position = random.Next(0, Emojis.Length - 10);
                dummies = Emojis[position..(position + 10)];
                answer = dummies[random.Next(0, 10)];
                break;
            case CaptchaType.String:
                for (var i = 0; i < 10; i++)
                {
                    dummies[i] = Strings[random.Next(0, Strings.Length - 1)];
                }
                answer = dummies[random.Next(0, 9)];
                break;
            case CaptchaType.Number:
                break;
        }

        var bitmap = new SKBitmap(64, 64);
        var canvas = new SKCanvas(bitmap);
        var background = new SKPaint { Color = new SKColor((byte) random.Next(), (byte) random.Next(), (byte) random.Next()) };
        var font = new SKPaint
        {
            Typeface = SKTypeface.FromFile(Path.Join(Directory.GetCurrentDirectory(), "CaptchaGeneration/NotoColorEmoji-Regular.ttf")),
            TextSize = 32
        };
        
        canvas.DrawRect(0, 0, 64, 64, background);
        canvas.DrawText(answer, 32, 32, font);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Webp, 80);
        var stream = new MemoryStream();
        data.SaveTo(stream);
        stream.Flush();

        return (answer, string.Join("", dummies), stream.ToArray());
    }
}
