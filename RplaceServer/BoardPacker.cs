using System.Buffers.Binary;
using RplaceServer.Types;

namespace RplaceServer;

public static class BoardPacker
{
    public static byte[] PackBoard(byte[] board, List<uint>? palette, int boardWidth)
    {
        
        var metadataLength = 4 + (palette?.Count ?? 0) * 4 + 2;
        var packedBoard = (Span<byte>) stackalloc byte[board.Length + metadataLength];
        board.CopyTo(packedBoard);

        var iteration = board.Length;
        BinaryPrimitives.WriteUInt32BigEndian(packedBoard[iteration..(iteration + 4)], (uint) boardWidth);
        iteration += 4;

        if (palette is not null)
        {
            for (var colour = board.Length + 4; colour < packedBoard.Length - 2; colour++)
            {
                BinaryPrimitives.WriteUInt32BigEndian(packedBoard[iteration..(iteration + 4)], palette[colour]);
                iteration += 4;
            }
        }
        
        BinaryPrimitives.WriteUInt16BigEndian(packedBoard[iteration..(iteration + 2)], (ushort) metadataLength);
        
        return packedBoard.ToArray();
    }

    public static UnpackedBoard UnpackBoard(byte[] packed)
    {
        var packedBoard = new Span<byte>(packed);
        
        var metadataLength =
            BinaryPrimitives.ReadUInt16BigEndian(packedBoard[^2..packedBoard.Length]);
        var boardLength = packedBoard.Length - metadataLength;

        var boardWidth = BinaryPrimitives.ReadUInt32BigEndian(packedBoard[boardLength..(boardLength + 4)]);

        var palette = new List<int>();
        for (var i = boardLength + 4; i < packedBoard.Length - 2; i++)
        {
            palette.Add((int) BinaryPrimitives.ReadUInt32BigEndian(packedBoard[i..(i+4)]));
        }

        var board = new byte[boardLength];
        packedBoard[..boardLength].CopyTo(board);

        return new UnpackedBoard(board, (int) boardWidth, palette);
    }

    public static byte[] RunLengthCompressBoard(byte[] board, uint[] palette)
    {
        // Our optimisations will be so negligible, there will be no point compressing
        if (palette.Length <= 254)
        {
            return board;
        }

        var newBoard = (Span<byte>) stackalloc byte[board.Length];
        var newBoardI = 0;
        var i = 0;
        
        while (i < board.Length)
        {
            var repeatedAfter = 0;
            
            for (var j = 0; j < 256 - palette.Length; i++)
            {
                if (board[i] == board[j])
                {
                    repeatedAfter++;
                }
                else
                {
                    break;
                }
            }

            newBoard[newBoardI] = board[i];
            newBoard[newBoardI + 1] = (byte) (palette.Length + repeatedAfter);
            newBoardI += 2;
            i += repeatedAfter;
        }

        // We make sure to slice it down so that it ends up *actually* smaller than input board
        return newBoard.Length < board.Length ? newBoard[..newBoardI].ToArray() : board;
    }
}