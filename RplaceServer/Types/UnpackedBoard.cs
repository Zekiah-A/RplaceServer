namespace RplaceServer.Types;

public record UnpackedBoard(byte[] Board, uint Width, List<uint> Palette);