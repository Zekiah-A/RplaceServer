namespace HTTPOfficial;

public record Configuration(int Port, bool UseHttps, string CertPath, string KeyPath, string SmtpHost, string EmailUsername, string EmailPassword, InstanceRange[] InstanceRanges);