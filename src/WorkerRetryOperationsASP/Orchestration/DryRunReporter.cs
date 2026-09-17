using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WorkerRetryOperationsASP.Configuration;
using WorkerRetryOperationsASP.Correlation;
using WorkerRetryOperationsASP.Io;
using WorkerRetryOperationsASP.Parsing;

namespace WorkerRetryOperationsASP.Orchestration;

/// <summary>
/// Modo de solo lectura: parsea el log y correlaciona candidatos a reintento,
/// pero no llama a SQL Server en absoluto (ni el SP ni la tabla reintentos_asp).
/// Útil para validar, antes de tener la cadena de conexión configurada, qué
/// cve_rastreo se reintentarían y con qué parámetros exactos.
/// </summary>
public sealed class DryRunReporter(
    ILogFileLocator logFileLocator,
    ILogParserService logParserService,
    ICorrelationService correlationService,
    IOptions<RetryWorkerOptions> options,
    ILogger<DryRunReporter> logger) : IDryRunReporter
{
    private readonly RetryWorkerOptions _options = options.Value;

    public async Task RunOnceAsync(CancellationToken ct)
    {
        var filePath = logFileLocator.GetTodayLogFilePath();
        if (filePath is null)
        {
            logger.LogInformation("[DRY RUN] No existe archivo de log para el día de hoy; no hay nada que mostrar.");
            return;
        }

        var (successes, errors) = await logParserService.ParseAsync(filePath, ct);
        var pending = correlationService.Correlate(successes, errors);

        if (!string.IsNullOrWhiteSpace(_options.OnlyCveRastreo))
        {
            pending = pending.Where(p => p.CveRastreo == _options.OnlyCveRastreo).ToList();
        }

        logger.LogInformation("[DRY RUN] Archivo analizado: {FilePath}", filePath);
        logger.LogInformation(
            "[DRY RUN] {SuccessCount} líneas SUCCESS, {ErrorCount} errores ProcessSTPWebhook, {PendingCount} candidatos a reintento. No se ejecuta ningún insert real.",
            successes.Count, errors.Count, pending.Count);

        if (pending.Count == 0)
        {
            return;
        }

        var index = 0;
        foreach (var item in pending)
        {
            index++;
            logger.LogInformation(
                "[DRY RUN] #{Index} cve_rastreo={CveRastreo}\n    @RequestId = {RequestId}\n    @EventType = {EventType}\n    @Status    = received\n    @Payload   = {Payload}",
                index, item.CveRastreo, item.CveRastreo, item.EventType, item.PayloadJson);
        }
    }
}
