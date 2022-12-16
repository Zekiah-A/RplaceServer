using System.Buffers.Binary;
using RplaceServer.Types;

namespace RplaceServer;

public static class BoardPacker
{
    public static byte[] PackBoard(byte[] board, List<int>? palette, int boardWidth)
    {
        
        var metadataLength = 4 + (palette?.Count ?? 0) * 4 + 2;
        var packedBoard = new byte[board.Length + metadataLength];
        Array.Copy(board, packedBoard, board.Length);

        var iteration = board.Length;
        BinaryPrimitives.WriteUInt32BigEndian(packedBoard.AsSpan()[iteration..(iteration + 4)], (uint) boardWidth);
        iteration += 4;

        if (palette is not null)
        {
            for (var colour = board.Length + 4; colour < packedBoard.Length; colour++)
            {
                BinaryPrimitives.WriteUInt32BigEndian(packedBoard.AsSpan()[iteration..(iteration + 4)], (uint) palette[colour]);
                iteration += 4;
            }
        }
        
        BinaryPrimitives.WriteUInt16BigEndian(packedBoard.AsSpan()[iteration..(iteration + 2)], (ushort) metadataLength);
        
        return packedBoard;
    }

    public static UnpackedBoard UnpackBoard(byte[] packedBoard)
    {
        var metadataLength =
            BinaryPrimitives.ReadUInt16BigEndian(packedBoard.AsSpan()[(packedBoard.Length - 2)..packedBoard.Length]);
        var boardLength = packedBoard.Length - metadataLength;

        var boardWidth = BinaryPrimitives.ReadUInt32BigEndian(packedBoard.AsSpan()[boardLength..(boardLength + 4)]);

        var palette = new List<int>();
        for (var i = boardLength + 2; i < packedBoard.Length - 2; i++)
        {
            palette.Add((int) BinaryPrimitives.ReadUInt32BigEndian(packedBoard.AsSpan()[i..(i+4)]));
        }

        var board = new byte[boardLength];
        Array.Copy(packedBoard, board, boardLength);

        return new UnpackedBoard(board, (int) boardWidth, palette);
    }
}