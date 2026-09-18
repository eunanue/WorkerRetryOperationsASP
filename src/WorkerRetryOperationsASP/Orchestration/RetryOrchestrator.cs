using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WorkerRetryOperationsASP.Configuration;
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
    IWebhookItravelStpRepository webhookItravelStpRepository,
    IOptions<RetryWorkerOptions> options,
    ILogger<RetryOrchestrator> logger) : IRetryOrchestrator
{
    private readonly RetryWorkerOptions _options = options.Value;

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

        if (!string.IsNullOrWhiteSpace(_options.OnlyCveRastreo))
        {
            pending = pending.Where(p => p.CveRastreo == _options.OnlyCveRastreo).ToList();
            logger.LogInformation("Filtro OnlyCveRastreo={CveRastreo} activo: {PendingCount} candidato(s) tras filtrar.",
                _options.OnlyCveRastreo, pending.Count);
        }
        else
        {
            logger.LogInformation("{PendingCount} candidatos a reintento tras correlacionar.", pending.Count);
        }

        var retried = 0;
        var skipped = 0;
        var failedAgain = 0;
        var alreadyInTarget = 0;

        foreach (var item in pending)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (await webhookRetryRepository.ExistsAsync(item.CveRastreo, item.EventType, ct))
                {
                    skipped++;
                    continue;
                }

                if (await webhookItravelStpRepository.ExistsAsync(item.CveRastreo, item.EventType, ct))
                {
                    // El insert original sí se aplicó del lado del servidor pese al timeout
                    // logueado por el middleware: no se llama al SP de nuevo (evita duplicar
                    // la fila, el índice sobre RequestId en webhooks_itravel_stp no es único).
                    // La validación es por RequestId + event_type.
                    await webhookRetryRepository.InsertAsync(item.CveRastreo, item.EventType, ct);
                    alreadyInTarget++;
                    logger.LogInformation(
                        "cve_rastreo={CveRastreo} EventType={EventType} ya existe en webhooks_itravel_stp; no se reintenta el insert, solo se registra en reintentos_asp.",
                        item.CveRastreo, item.EventType);
                    continue;
                }

                var ok = await stpWebhookInserter.TryInsertAsync(item.CveRastreo, item.EventType, item.PayloadJson, ct);
                if (ok)
                {
                    await webhookRetryRepository.InsertAsync(item.CveRastreo, item.EventType, ct);
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

        logger.LogInformation(
            "Corrida finalizada: {Retried} reintentados con éxito, {AlreadyInTarget} ya existían en webhooks_itravel_stp, {Skipped} ya registrados previamente, {FailedAgain} fallaron nuevamente.",
            retried, alreadyInTarget, skipped, failedAgain);
    }
}
