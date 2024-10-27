namespace RplaceServer.Types;

public record UnpackedBoard(byte[] Board, uint Width, uint Height, List<uint> Palette);