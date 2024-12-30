using AuthOfficial.DataModel;

namespace AuthOfficial.ApiModel;

public record PostsResponse(int Count, List<Post> Posts);