namespace RplaceServer.Types;

public record UnpackedBoard(byte[] Board, int Width, List<uint> Palette);