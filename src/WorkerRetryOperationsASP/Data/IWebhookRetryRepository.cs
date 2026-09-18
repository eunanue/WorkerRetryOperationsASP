namespace WorkerRetryOperationsASP.Data;

public interface IWebhookRetryRepository
{
    Task<bool> ExistsAsync(string cveRastreo, string eventType, CancellationToken ct);
    Task InsertAsync(string cveRastreo, string eventType, CancellationToken ct);
}
