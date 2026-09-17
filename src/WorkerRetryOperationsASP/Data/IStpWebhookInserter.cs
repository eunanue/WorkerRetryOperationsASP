namespace WorkerRetryOperationsASP.Data;

public interface IStpWebhookInserter
{
    Task<bool> TryInsertAsync(string requestId, string eventType, string payloadJson, CancellationToken ct);
}
