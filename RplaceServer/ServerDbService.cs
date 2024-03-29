using LiteDB;
using RplaceServer.Types;

namespace RplaceServer;

public class ServerDbService
{
    public LiteDatabase Database;
    public ILiteCollection<LiveChatMessage> LiveChatMessages;
    public ILiteCollection<LiveChatMessage> LiveChatReplies;
    public ILiteCollection<LiveChatMessage> PlaceChatMessages;
    
    public ServerDbService(string databasePath)
    {
        Database = new LiteDatabase(databasePath);
        LiveChatMessages = Database.GetCollection<LiveChatMessage>("live_chat_messages");
        LiveChatReplies = Database.GetCollection<LiveChatMessage>("live_chat_replies");
        PlaceChatMessages = Database.GetCollection<LiveChatMessage>("place_chat_messages");
    }
}