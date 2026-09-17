using Microsoft.Extensions.Logging;
using WorkerRetryOperationsASP.Correlation;
using WorkerRetryOperationsASP.Data;
using WorkerRetryOperationsASP.Io;
using WorkerRetryOperationsASP.Parsing;

namespace WorkerRetryOperationsASP.Orchestration;

public sealed class RetryOrchestrator(
    ILogFileLocator logFileLocator,
    ILogParserService logParserService,
    ICorrelationService correlationService,
    IStpWebhookInserter stpWebhookInserter,
    IWebhookRetryRepository webhookRetryRepository,
    ILogger<RetryOrchestrator> logger) : IRetryOrchestrator
{
    public async Task RunOnceAsync(CancellationToken ct)
    {
        var filePath = logFileLocator.GetTodayLogFilePath();
        if (filePath is null)
        {
            logger.LogInformation("No existe archivo de log para el día de hoy; no hay nada que procesar.");
            return;
        }

        var (successes, errors) = await logParserService.ParseAsync(filePath, ct);
        logger.LogInformation("Archivo {FilePath}: {SuccessCount} líneas SUCCESS, {ErrorCount} errores ProcessSTPWebhook detectados.",
            filePath, successes.Count, errors.Count);

        var pending = correlationService.Correlate(successes, errors);
        logger.LogInformation("{PendingCount} candidatos a reintento tras correlacionar.", pending.Count);

        var retried = 0;
        var skipped = 0;
        var failedAgain = 0;

        foreach (var item in pending)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (await webhookRetryRepository.ExistsAsync(item.CveRastreo, ct))
                {
                    skipped++;
                    continue;
                }

                var ok = await stpWebhookInserter.TryInsertAsync(item.CveRastreo, item.EventType, item.PayloadJson, ct);
                if (ok)
                {
                    await webhookRetryRepository.InsertAsync(item.CveRastreo, ct);
                    retried++;
                    logger.LogInformation("Reintento exitoso para cve_rastreo={CveRastreo} EventType={EventType}.", item.CveRastreo, item.EventType);
                }
                else
                {
                    failedAgain++;
                    logger.LogWarning("Reintento falló nuevamente para cve_rastreo={CveRastreo}; se reintentará en la próxima corrida.", item.CveRastreo);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fallo inesperado procesando el ítem cve_rastreo={CveRastreo}; se continúa con el siguiente.", item.CveRastreo);
            }
        }

        logger.LogInformation("Corrida finalizada: {Retried} reintentados con éxito, {Skipped} ya registrados previamente, {FailedAgain} fallaron nuevamente.",
            retried, skipped, failedAgain);
    }
}
