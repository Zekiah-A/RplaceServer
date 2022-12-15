using FFMpegCore.Pipes;
using SkiaSharp;

namespace PlaceHttpsServer;

public class SKBitmapFrame : IVideoFrame, IDisposable
{
    public int Width => source.Width;
    public int Height => source.Height;
    public string Format => "bgra";
    
    private readonly SKBitmap source;

    public SKBitmapFrame(SKBitmap bmp)
    {
        if (bmp.ColorType != SKColorType.Bgra8888)
        {
            throw new NotImplementedException("only 'bgra' colour type is supported");
        }
        
        source = bmp;
    }

    public void Dispose() => source.Dispose();

    public void Serialize(Stream stream) => stream.Write(source.Bytes);

    public async Task SerializeAsync(Stream stream, CancellationToken token) => 
        await stream.WriteAsync(source.Bytes, token).ConfigureAwait(false);
}