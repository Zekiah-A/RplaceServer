using FFMpegCore;
using FFMpegCore.Pipes;
using RplaceServer;
using SkiaSharp;

namespace PlaceHttpsServer;

/// <summary>
/// Dependencies:
/// Windows: ffmpeg,
/// MacOS: ffmpeg, mono-libgdiplus,
/// Linux: ffmpeg libgdiplus,
/// </summary>
internal static class TimelapseGenerator
{
    private static readonly SKColor[] Colours =
    {
        new(109, 0, 26), new(190, 0, 57), new(255, 69, 0), new(255, 168, 0),
        new(255, 214, 53), new(255, 248, 184), new(0, 163, 104), new(0, 204, 120),
        new(126, 237, 86), new(0, 117, 111), new(0, 158, 170), new(0, 204, 192),
        new(36, 80, 164), new(54, 144, 234), new(81, 233, 244), new(73, 58, 193),
        new(106, 92, 255), new(148, 179, 255), new(129, 30, 159), new(180, 74, 192),
        new(228, 171, 255), new(222, 16, 127), new(255, 56, 129), new(255, 153, 170),
        new (109, 72, 47), new(156, 105, 38), new(255, 180, 112), new(0, 0, 0),
        new(81, 82, 82), new(137, 141, 144), new(212, 215, 217), new(255, 255, 255)
    };
    
    public static async Task<Stream> GenerateTimelapseAsync(TimelapseInformation info, GameData gameData)
    {
        var backups = Directory.GetFiles(gameData.CanvasFolder)
            .SkipWhile(backup => Path.GetFileName(backup) != info.BackupStart)
            .TakeWhile(backup => Path.GetFileName(backup) != info.BackupEnd)
            .ToArray();
        
        if (info.Reverse)
        {
            Array.Reverse(backups);
        }

        var frames = new List<SKBitmapFrame>();

        foreach (var path in backups)
        {
            if (!File.Exists(path))
            {
                continue;
            }

            using var bitmap = new SKBitmap(info.EndX - info.StartX, info.EndY - info.StartY);
            var unpacked = BoardPacker.UnpackBoard(await File.ReadAllBytesAsync(path));

            var i = unpacked.Width * info.StartY + info.StartX;
            while (i < unpacked.Board.Length)
            {
                bitmap.SetPixel(i % unpacked.Width - info.StartX, i / unpacked.Width - info.StartY, Colours[unpacked.Board[i]]);
                i++;

                // If we exceed width, go to next row, otherwise continue
                if (i % unpacked.Width < info.EndX)
                {
                    continue; 
                }

                // If we exceed end bottom, we are done drawing this
                if (i / unpacked.Width == info.EndY - 1)
                {
                    break; 
                }
                
                i += unpacked.Width - (info.EndX - info.StartX);
            }

            frames.Add(new SKBitmapFrame(bitmap));
        }
        
        using var stream = new MemoryStream();
        var framesSource = new RawVideoPipeSource(frames) { FrameRate = info.Fps };
        var outSink = new StreamPipeSink(stream);
        await FFMpegArguments
            .FromPipeInput(framesSource)
            .OutputToPipe(outSink, options => options
                .WithVideoCodec("libvpx-vp9")
                .ForceFormat("webm"))
            .ProcessAsynchronously();
        
        return stream;
    }
}
