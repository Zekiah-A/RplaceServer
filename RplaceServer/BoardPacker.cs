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

        var position = board.Length;
        BinaryPrimitives.WriteUInt32BigEndian(packedBoard[position..(position + 4)], (uint) boardWidth);
        position += 4;

        if (palette is not null)
        {
            foreach (var colour in palette)
            {
                BinaryPrimitives.WriteUInt32BigEndian(packedBoard[position..(position + 4)], colour);
                position += 4;
            }
        }
        
        BinaryPrimitives.WriteUInt16BigEndian(packedBoard[position..(position + 2)], (ushort) metadataLength);
        return packedBoard.ToArray();
    }

    public static UnpackedBoard UnpackBoard(byte[] packed)
    {
        var packedBoard = new Span<byte>(packed);
        
        var metadataLength =
            BinaryPrimitives.ReadUInt16BigEndian(packedBoard[^2..packedBoard.Length]);
        var boardLength = packedBoard.Length - metadataLength;

        var boardWidth = BinaryPrimitives.ReadUInt32BigEndian(packedBoard[boardLength..(boardLength + 4)]);

        var palette = new List<uint>();
        for (var i = boardLength + 4; i < packedBoard.Length - 2; i += 4)
        {
            palette.Add(BinaryPrimitives.ReadUInt32BigEndian(packedBoard[i..(i + 4)]));
        }

        var board = new byte[boardLength];
        packedBoard[..boardLength].CopyTo(board);

        return new UnpackedBoard(board, (int) boardWidth, palette);
    }

    public static byte[] RunLengthCompressBoard(byte[] board)
    {
        var newBoard = new byte[board.Length];
        var newBoardI = 0;

        var i = 0;
        while (i < board.Length)
        {
            var repeated = 0;
            for (var j = 0; j < Math.Abs(Math.Min(256, board.Length - i)); j++)
            {
                if (board[i] == board[i + j])
                {
                    repeated++;    
                }
                else
                {
                    break;
                }
            }

            newBoard[newBoardI] = board[i];
            // Byte can only hold max 255, so we subtract one from repeated (256), and treat it as though it is +1
            newBoard[newBoardI + 1] = (byte) (repeated - 1);
            
            newBoardI += 2;
            i += repeated;
        }

        return newBoardI < board.Length ? newBoard[..newBoardI] : board;
    }
}