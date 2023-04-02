namespace HTTPOfficial;

public record InstanceRange(string InstanceIp, IntRange Range);
public record IntRange(int Start, int End);