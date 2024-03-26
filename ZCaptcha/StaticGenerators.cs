using System.Runtime.InteropServices;
using System.Text;

namespace ZCaptcha;

#pragma warning disable CS8500
// dotnet publish --configuration Release /p:NativeLib=Shared --use-current-runtime -p:PublishAot=true,StripSymbols=true
// TEST: readelf -Ws --dyn-syms ZCaptcha.so
public static unsafe class StaticGenerators
{
    private static EmojiCaptchaGenerator emojiGenerator;
    private static TextCaptchaGenerator textGenerator;

    // provided char* must be UTF-16
    [UnmanagedCallersOnly(EntryPoint = "initialise")]
    public static int Initialise(char* fontPath)
    {
        var path = Marshal.PtrToStringUTF8((IntPtr)fontPath);
        if (path == null)
        {
            return -1;
        }

        emojiGenerator = new EmojiCaptchaGenerator(path);
        textGenerator = new TextCaptchaGenerator(path);
        return 0;
    }

    public static byte* ToUnmanagedUTF8(string text)
    {
        var utf8Bytes =  Encoding.Convert(Encoding.Default, Encoding.UTF8, Encoding.Default.GetBytes(text));
        var refBytes = (byte*) NativeMemory.Alloc((nuint) utf8Bytes.Length + 1);
        fixed(byte* fixedBytes = utf8Bytes)
        {
            NativeMemory.Copy(fixedBytes, refBytes, (nuint) utf8Bytes.Length);
        }
        refBytes[utf8Bytes.Length] = (byte) '\0'; // null terminate

        return refBytes;
    }

    public static NativeGenerationResult* UnmanagedNativeResultFrom(ref CaptchaGenerationResult result)
    {
        var refResult = (NativeGenerationResult*) NativeMemory.Alloc((nuint) sizeof(NativeGenerationResult));
        refResult->Answer = ToUnmanagedUTF8(result.Answer);
        refResult->Dummies = ToUnmanagedUTF8(result.Dummies);
        
        var refImgData = (byte*) NativeMemory.Alloc((nuint) result.ImageData.Length);
        fixed(byte* fixedImgData = result.ImageData)
        {
            NativeMemory.Copy(fixedImgData, refImgData, (nuint) result.ImageData.Length);
        }
        refResult->ImageData = refImgData;
        refResult->ImageDataLength = result.ImageData.Length;
        return refResult;
    }

    [UnmanagedCallersOnly(EntryPoint = "gen_emoji_captcha")]
    public static NativeGenerationResult* GenEmojiCaptcha()
    {
        // Copy to heap
        var result = emojiGenerator.Generate();
        return UnmanagedNativeResultFrom(ref result);
    }

    [UnmanagedCallersOnly(EntryPoint = "gen_text_captcha")]
    public static NativeGenerationResult* GenTextCaptcha()
    {
        var result = textGenerator.Generate();
        return UnmanagedNativeResultFrom(ref result);
    }

    [UnmanagedCallersOnly(EntryPoint = "dispose_result")]
    public static void DisposeResult(NativeGenerationResult* result)
    {
        if (result == null)
        {
            return;
        }

        NativeMemory.Free(result->Dummies);
        NativeMemory.Free(result->Answer);
        NativeMemory.Free(result->ImageData);
        NativeMemory.Free(result);
    }
}
#pragma warning restore CS8500
