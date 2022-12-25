namespace RplaceServer.Types;

public enum ServerPacket
{
    Palette = 0,
    CanvasInfo = 1,
    PlayerCount = 3,
    PixelPlace = 6,
    RejectPixel = 7,
    ChatMessage = 15,
    Captcha = 16
}