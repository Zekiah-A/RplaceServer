namespace RplaceServer.Types;

public record UnpackedBoard(byte[] Board, int Width, List<int> Palette);