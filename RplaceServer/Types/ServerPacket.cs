namespace RplaceServer.Types;

public enum ServerPacket
{
    InitialInfo = 1,
    RejectPixel = 7,
    ChatMessage = 15,
    CaptchaSuccess = 16
}