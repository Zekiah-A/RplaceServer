using HTTPOfficial.DataModel;

namespace HTTPOfficial.ApiModel;

public record PostsResponse(int Count, List<Post> Posts);