using System.Buffers.Binary;
using RplaceServer.Types;

namespace RplaceServer;

public static class BoardPacker
{
    public static byte[] PackBoard(byte[] board, List<uint>? palette, uint boardWidth, uint boardHeight, long? creationDate = null)
    {
        var unixTime = creationDate ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var metadataLength = sizeof(long) + sizeof(uint) * 2 + sizeof(byte) + (palette?.Count ?? 0) * sizeof(uint);
        var packedBoard = (Span<byte>)stackalloc byte[metadataLength + board.Length];
    
        var position = 0;
        BinaryPrimitives.WriteInt64BigEndian(packedBoard[position..], unixTime);
        position += sizeof(long);
        BinaryPrimitives.WriteUInt32BigEndian(packedBoard[position..], boardWidth);
        position += sizeof(uint);
        BinaryPrimitives.WriteUInt32BigEndian(packedBoard[position..], boardHeight);
        position += sizeof(uint);

        packedBoard[position++] = (byte)(palette?.Count ?? 0);
        if (palette is not null)
        {
            foreach (var colour in palette)
            {
                BinaryPrimitives.WriteUInt32BigEndian(packedBoard[position..(position + 4)], colour);
                position += sizeof(uint);
            }
        }
    
        board.CopyTo(packedBoard[position..]);
        return packedBoard.ToArray();
    }

    public static UnpackedBoard UnpackBoard(byte[] packed)
    {
        var packedBoard = new Span<byte>(packed);

        var position = 0;
        var unixTime = BinaryPrimitives.ReadInt64BigEndian(packedBoard[position..]);
        position += sizeof(long);
        var boardWidth = BinaryPrimitives.ReadUInt32BigEndian(packedBoard[position..]);
        position += sizeof(uint);
        var boardHeight = BinaryPrimitives.ReadUInt32BigEndian(packedBoard[position..]);
        position += sizeof(uint);

        var paletteLength = packedBoard[position++];
        var palette = new List<uint>();
        for (var i = 0; i < paletteLength; i++)
        {
            palette.Add(BinaryPrimitives.ReadUInt32BigEndian(packedBoard[position..]));
            position += sizeof(uint);
        }

        return new UnpackedBoard(packedBoard[position..].ToArray(), boardWidth, boardHeight, palette);
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