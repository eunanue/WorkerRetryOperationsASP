namespace WorkerRetryOperationsASP.Orchestration;

public interface IRetryOrchestrator
{
    Task RunOnceAsync(CancellationToken ct);
}
