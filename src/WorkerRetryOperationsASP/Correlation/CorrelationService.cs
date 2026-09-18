using Microsoft.Extensions.Logging;
using WorkerRetryOperationsASP.Models;

namespace WorkerRetryOperationsASP.Correlation;

public sealed class CorrelationService(ILogger<CorrelationService> logger) : ICorrelationService
{
    public IReadOnlyList<PendingRetryItem> Correlate(IReadOnlyList<LogSuccessRecord> successes, IReadOnlyList<LogErrorRecord> errors)
    {
        // Un mismo RequestId/cveRastreo puede tener varios eventos con payloads distintos:
        // el payload correcto se busca por (RequestId, EventType).
        var byCveRastreoAndEvent = successes
            .GroupBy(s => Key(s.CveRastreo, s.EventType))
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.LineNumber).ToList());

        var result = new List<PendingRetryItem>();

        foreach (var error in errors)
        {
            if (!byCveRastreoAndEvent.TryGetValue(Key(error.RequestId, error.EventType), out var candidates))
            {
                logger.LogWarning(
                    "No se encontró SUCCESS correlacionado por RequestId y EventType para RequestId={RequestId} EventType={EventType} (línea {LineNumber}); se omite este reintento.",
                    error.RequestId, error.EventType, error.LineNumber);
                continue;
            }

            // Última ocurrencia SUCCESS anterior al error (por número de línea).
            var match = candidates.LastOrDefault(s => s.LineNumber < error.LineNumber);
            if (match is null)
            {
                logger.LogWarning(
                    "No se encontró SUCCESS anterior al error (mismo RequestId y EventType) para RequestId={RequestId} EventType={EventType} (línea {LineNumber}); se omite este reintento.",
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

    private static string Key(string requestId, string eventType) =>
        $"{requestId}\u001f{eventType.Trim().ToUpperInvariant()}";
}
