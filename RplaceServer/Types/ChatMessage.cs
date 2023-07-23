namespace RplaceServer.Types;

public class LiveChatMessage
{
    public uint MessageId;
    public DateTime SendDate;
    public string Message;
    public string Name;
    public string Channel;
    public uint RepliesTo;

    public UidType SenderType;
    public string SenderUid;
}

public class LiveChatReaction
{
    public uint MessageId;
    public string Reaction;
    
    public UidType ReacterType;
    public string ReacterUid;
}

public class PlaceChat
{
    public uint MessageId;
    public DateTime SendDate;
    public string Message;
    public string Name;
    public uint X;
    public uint Y;

    public UidType SenderType;
    public string SenderUid;
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