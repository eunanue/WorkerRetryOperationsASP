namespace WorkerRetryOperationsASP.Models;

public sealed class LogSuccessRecord
{
    public required int LineNumber { get; init; }
    public required DateTime Timestamp { get; init; }
    public required string CveRastreo { get; init; }
    public required string PayloadJson { get; init; }
}
