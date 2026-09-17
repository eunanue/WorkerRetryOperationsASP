using Microsoft.Extensions.Logging;
using WorkerRetryOperationsASP.Models;

namespace WorkerRetryOperationsASP.Correlation;

public sealed class CorrelationService(ILogger<CorrelationService> logger) : ICorrelationService
{
    public IReadOnlyList<PendingRetryItem> Correlate(IReadOnlyList<LogSuccessRecord> successes, IReadOnlyList<LogErrorRecord> errors)
    {
        var byCveRastreo = successes
            .GroupBy(s => s.CveRastreo)
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.LineNumber).ToList());

        var result = new List<PendingRetryItem>();

        foreach (var error in errors)
        {
            if (!byCveRastreo.TryGetValue(error.RequestId, out var candidates))
            {
                logger.LogWarning(
                    "No se encontró SUCCESS correlacionado para RequestId={RequestId} EventType={EventType} (línea {LineNumber}); se omite este reintento.",
                    error.RequestId, error.EventType, error.LineNumber);
                continue;
            }

            // Última ocurrencia SUCCESS anterior al error (por número de línea).
            var match = candidates.LastOrDefault(s => s.LineNumber < error.LineNumber);
            if (match is null)
            {
                logger.LogWarning(
                    "No se encontró SUCCESS anterior al error para RequestId={RequestId} EventType={EventType} (línea {LineNumber}); se omite este reintento.",
                    error.RequestId, error.EventType, error.LineNumber);
                continue;
            }

            result.Add(new PendingRetryItem
            {
                CveRastreo = error.RequestId,
                EventType = error.EventType,
                PayloadJson = match.PayloadJson,
                ErrorLineNumber = error.LineNumber,
                SuccessLineNumber = match.LineNumber
            });
        }

        return result;
    }
}
