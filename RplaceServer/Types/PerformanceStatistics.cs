namespace RplaceServer;

public record PerformanceStatistics(long MemoryUsage, double CpuUsage, int BackupCount, long BackupDirectorySize);