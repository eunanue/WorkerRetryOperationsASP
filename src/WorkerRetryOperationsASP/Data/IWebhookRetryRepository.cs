namespace WorkerRetryOperationsASP.Data;

public interface IWebhookRetryRepository
{
    Task<bool> ExistsAsync(string cveRastreo, CancellationToken ct);
    Task InsertAsync(string cveRastreo, CancellationToken ct);
}
