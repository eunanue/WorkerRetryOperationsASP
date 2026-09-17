namespace WorkerRetryOperationsASP.Models;

public sealed class PendingRetryItem
{
    public required string CveRastreo { get; init; }
    public required string EventType { get; init; }
    public required string PayloadJson { get; init; }
    public required int ErrorLineNumber { get; init; }
    public required int SuccessLineNumber { get; init; }
}
