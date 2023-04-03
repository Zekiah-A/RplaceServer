namespace HTTPOfficial;

public record AccountData(string Username, string Password, string Email, int AccountTier, List<int> Instances);