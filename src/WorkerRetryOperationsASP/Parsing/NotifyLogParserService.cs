using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WorkerRetryOperationsASP.Models;

namespace WorkerRetryOperationsASP.Parsing;

public sealed partial class NotifyLogParserService(ILogger<NotifyLogParserService> logger) : ILogParserService
{
    private const string TimestampFormat = "yyyy-MM-dd HH:mm:ss";

    public async Task<(List<LogSuccessRecord> Successes, List<LogErrorRecord> Errors)> ParseAsync(string filePath, CancellationToken ct)
    {
        var successes = new List<LogSuccessRecord>();
        var errors = new List<LogErrorRecord>();

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);

        var lineNumber = 0;
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            lineNumber++;

            var lineMatch = LinePattern().Match(line);
            if (!lineMatch.Success)
            {
                // Línea de continuación (stack trace) sin el prefijo de timestamp/nivel: se ignora.
                continue;
            }

            if (!DateTime.TryParseExact(lineMatch.Groups["ts"].Value, TimestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp))
            {
                logger.LogWarning("Línea {LineNumber}: timestamp inválido, se descarta.", lineNumber);
                continue;
            }

            var level = lineMatch.Groups["level"].Value;
            var rest = lineMatch.Groups["rest"].Value;

            if (level == "ERROR")
            {
                TryParseError(rest, lineNumber, timestamp, errors);
            }
            else
            {
                TryParseSuccess(rest, lineNumber, timestamp, successes);
            }
        }

        return (successes, errors);
    }

    private void TryParseError(string rest, int lineNumber, DateTime timestamp, List<LogErrorRecord> errors)
    {
        var match = ProcessSTPWebhookErrorPattern().Match(rest);
        if (!match.Success)
        {
            // No es un error "Tipo A" (ej. es el Tipo B InsertCreateOrderASP_SPAsync, sin identificador): fuera de alcance.
            return;
        }

        var eventType = match.Groups["eventType"].Value.Trim();
        var requestId = match.Groups["requestId"].Value.Trim();

        if (string.IsNullOrWhiteSpace(requestId))
        {
            logger.LogWarning("Línea {LineNumber}: error ProcessSTPWebhook sin RequestId, no se puede correlacionar; se descarta.", lineNumber);
            return;
        }

        errors.Add(new LogErrorRecord
        {
            LineNumber = lineNumber,
            Timestamp = timestamp,
            EventType = eventType,
            RequestId = requestId
        });
    }

    private void TryParseSuccess(string rest, int lineNumber, DateTime timestamp, List<LogSuccessRecord> successes)
    {
        JObject json;
        try
        {
            json = JObject.Parse(rest);
        }
        catch (JsonReaderException ex)
        {
            logger.LogWarning(ex, "Línea {LineNumber}: JSON inválido en línea SUCCESS, se descarta.", lineNumber);
            return;
        }

        var eventMatch = SuccessEventTypePattern().Match(json["Message"]?.Value<string>() ?? string.Empty);
        if (!eventMatch.Success)
        {
            logger.LogWarning("Línea {LineNumber}: no se pudo extraer el EventType del Message de la línea SUCCESS, se descarta.", lineNumber);
            return;
        }

        if (!PayloadNotifyNormalizer.TryNormalize(json, out var payloadJson, out var cveRastreo))
        {
            logger.LogWarning("Línea {LineNumber}: no se pudo extraer cveRastreo/PayloadNotify de la línea SUCCESS, se descarta.", lineNumber);
            return;
        }

        successes.Add(new LogSuccessRecord
        {
            LineNumber = lineNumber,
            Timestamp = timestamp,
            EventType = eventMatch.Groups["eventType"].Value,
            CveRastreo = cveRastreo,
            PayloadJson = payloadJson
        });
    }

    [GeneratedRegex(@"^(?<ts>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}) \[(?<level>SUCCESS|ERROR)\] (?<rest>.*)$")]
    private static partial Regex LinePattern();

    // Message de las líneas SUCCESS: "Webhook {EventType} recibido exitosamente".
    [GeneratedRegex(@"^Webhook (?<eventType>\S+) recibido exitosamente")]
    private static partial Regex SuccessEventTypePattern();

    [GeneratedRegex(@"^Error ProcessSTPWebhook - EventType: (?<eventType>.*?) - RequestId: (?<requestId>.*?) - Error: (?<error>.*)$")]
    private static partial Regex ProcessSTPWebhookErrorPattern();
}
