namespace RplaceServer.Types;

public enum ClientPacket : byte
{
    PixelPlace = 4,
    ChatMessage = 15,
    CaptchaSubmit = 16,
    ModAction = 98,
    Rollback = 99,
}