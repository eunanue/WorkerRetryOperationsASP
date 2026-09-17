namespace WorkerRetryOperationsASP.Data;

/// <summary>
/// Consulta de solo lectura contra la tabla de negocio real
/// (MiddlewareSTP.dbo.webhooks_itravel_stp) que llena el SP
/// dbo.usp_WebhookItravel_InsertASP. Se usa para evitar reintentar (y duplicar)
/// un insert que, pese al timeout logueado por el middleware, sí llegó a
/// aplicarse del lado del servidor.
/// </summary>
public interface IWebhookItravelStpRepository
{
    Task<bool> ExistsAsync(string requestId, CancellationToken ct);
}
