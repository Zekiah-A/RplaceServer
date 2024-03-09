namespace RplaceServer.Types;

public record PerformanceStatistics(long MemoryUsage, double CpuUsage, int BackupCount, long BackupDirectorySize);