namespace AuthOfficial.DataModel;

public class BannedContent
{
    public int Id { get; set; }
    
    public string Hash { get; set; }
    public ContentHashType HashType { get; set; }
    public ContentFileType FileType { get; set; }
    public string Reason { get; set; }
    public DateTime Date { get; set; }
    
    public int? ModeratorId { get; set; } = null!;
    public Account? Moderator { get; set; } = null!;
    
    public BannedContent() { }

    public BannedContent(string hash, ContentHashType hashType, ContentFileType fileType,
        string reason, DateTime? date, int? moderatorId = null)
    {
        Hash = hash;
        HashType = hashType;
        FileType = fileType;
        Reason = reason;
        ModeratorId = moderatorId;
        Date = date ?? DateTime.Now;
    }
}

public enum ContentFileType
{
    Image,
    Audio,
    Other
}

public enum ContentHashType
{
    Perceptual,
    MD5Checksum
}