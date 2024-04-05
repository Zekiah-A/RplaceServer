namespace RplaceServer.Types;

public class LiveChatMessage
{
    public int MessageId;
    public int SenderId;
    public DateTime SendDate;
    public string Message;
    public string Channel;
    public int RepliesTo;
}

public class LiveChatReaction
{
    public int MessageId;
    public string Reaction;
    public int ReacterId;
}

public class PlaceChat
{
    public int MessageId;
    public int SenderId;
    public DateTime SendDate;
    public string Message;
    public int X;
    public int Y;
}

// A chat message packet (server -> client) is made up of the following. And chat messages may follow this style.  
// `uint` messageID
// `byte` messageType
// `long` sendDate (unix time)
// message (either `short, byte[]` or dataproto string)
// name (either `byte, byte[]` or dataproto string)
// senderUid (either 7 bits, 1 bit uidType, byte[] or dataproto string) _________ // Represents either the sender account GUID or their IP encrypted with a key only the server knows of
// |                                                                            |
// |                                                                            |
// v                                                                            v
// messagetype == MessageType.Live                                              messageType == MessageType.Place
// `byte` reactionsCount                                                        `uint` position
// reactions (either byte[] comma separated string or idk)
// channel (either byte, byte[] or dataproto string)
// `uint32` repliesTo (messageId)