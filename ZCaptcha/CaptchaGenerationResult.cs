using System.Runtime.InteropServices;

namespace ZCaptcha;

public record struct CaptchaGenerationResult
(   string Answer,
    string Dummies,
    byte[] ImageData
);


[StructLayout(LayoutKind.Explicit, Size=28)]
public unsafe struct NativeGenerationResult
{
    [FieldOffset(0)]public byte* Answer; // UTF-8 null terminated, u64
    [FieldOffset(8)]public byte* Dummies; // UTF-8 null terminated, u64
    [FieldOffset(16)]public byte* ImageData; // u64
    [FieldOffset(24)]public int ImageDataLength; // u32
};