namespace WorkerRetryOperationsASP.Orchestration;

public interface IDryRunReporter
{
    Task RunOnceAsync(CancellationToken ct);
}
