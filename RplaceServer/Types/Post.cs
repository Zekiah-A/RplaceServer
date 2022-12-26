using System.Text.Json.Serialization;

namespace RplaceServer.Types;

public record Post
(
    string Username,
    string Title,
    string Description
)
{
    [JsonInclude] public int? Upvotes;
    [JsonInclude] public int? Downvotes;
    [JsonInclude] public string? ContentPath;
    [JsonInclude] public DateTime? CreationDate;
};