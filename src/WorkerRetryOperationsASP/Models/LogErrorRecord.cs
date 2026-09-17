namespace WorkerRetryOperationsASP.Models;

public sealed class LogErrorRecord
{
    public required int LineNumber { get; init; }
    public required DateTime Timestamp { get; init; }
    public required string EventType { get; init; }
    public required string RequestId { get; init; }
}
