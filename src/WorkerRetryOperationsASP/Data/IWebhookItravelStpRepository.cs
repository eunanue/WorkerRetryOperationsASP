namespace WorkerRetryOperationsASP.Data;

/// <summary>
/// Consulta de solo lectura contra la tabla de negocio real
/// (MiddlewareSTP.dbo.webhooks_itravel_stp) que llena el SP
/// dbo.usp_WebhookItravel_InsertASP. Se usa para evitar reintentar (y duplicar)
/// un insert que, pese al timeout logueado por el middleware, sí llegó a
/// aplicarse del lado del servidor. La coincidencia es por RequestId y
/// event_type: un mismo RequestId puede tener filas de distintos tipos de evento.
/// </summary>
public interface IWebhookItravelStpRepository
{
    Task<bool> ExistsAsync(string requestId, string eventType, CancellationToken ct);
}
