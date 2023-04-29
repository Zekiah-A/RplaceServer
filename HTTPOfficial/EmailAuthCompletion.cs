namespace HTTPOfficial;

public record EmailAuthCompletion(TaskCompletionSource<bool> TaskSource, DateTime StartDate);