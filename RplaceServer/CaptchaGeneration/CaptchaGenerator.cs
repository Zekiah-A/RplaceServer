using Microsoft.VisualBasic;
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
                var position = random.Next(0, Emojis.Length - 40);
                dummies = Emojis[position..(position + 10)];
                answer = dummies[random.Next(0, 10)];
                break;
            case CaptchaType.String:
                Buffer.BlockCopy(Strings, random.Next(0, Emojis.Length - 40), dummies, 0, 40);
                answer = dummies[random.Next(0, 10)];
                break;
            case CaptchaType.Number:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }

        var bitmap = new SKBitmap(64, 64);
        var canvas = new SKCanvas(bitmap);
        var background = new SKPaint { Color = new SKColor((byte) random.Next(), (byte) random.Next(), (byte) random.Next()) };
        var text = SKTextBlob.Create(answer, new SKFont(SKTypeface.Default, 32, 32));
        
        canvas.DrawRect(0, 0, 64, 64, background);
        canvas.DrawText(text, 0, 0, new SKPaint { Color = SKColors.Black });

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Webp, 80);
        var stream = new MemoryStream();
        data.SaveTo(stream);
        stream.Flush();

        return (answer, string.Join('\n', dummies), stream.ToArray());
    }
}
