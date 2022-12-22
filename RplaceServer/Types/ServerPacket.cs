namespace RplaceServer.Types;

public enum ServerPacket
{
    Palette = 0,
    CanvasInfo = 1,
    Changes = 2,
    GameInfo = 3,
    RejectPixel = 7,
    ChatMessage = 15,
    CaptchaSuccess = 16
}