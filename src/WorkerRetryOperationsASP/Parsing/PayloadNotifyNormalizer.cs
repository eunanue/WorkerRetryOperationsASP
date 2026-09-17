using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WorkerRetryOperationsASP.Parsing;

/// <summary>
/// El campo "PayloadNotify" del log viene en dos formas distintas según el
/// tipo de webhook: como objeto JSON anidado directo (ej. /webhook/eiyu/abonos)
/// o como string con JSON escapado dentro (ej. /webhook/eiyu/actualiza_pago).
/// Ambas se normalizan al mismo string JSON canónico, listo para usarse como @Payload.
/// </summary>
public static class PayloadNotifyNormalizer
{
    public static bool TryNormalize(JObject successLine, out string payloadJson, out string cveRastreo)
    {
        payloadJson = string.Empty;
        cveRastreo = string.Empty;

        var payloadToken = successLine["PayloadNotify"];
        if (payloadToken is null || payloadToken.Type == JTokenType.Null)
        {
            return false;
        }

        JObject inner;
        if (payloadToken.Type == JTokenType.Object)
        {
            inner = (JObject)payloadToken;
            payloadJson = inner.ToString(Formatting.None);
        }
        else if (payloadToken.Type == JTokenType.String)
        {
            var raw = payloadToken.Value<string>();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            payloadJson = raw;
            inner = JObject.Parse(raw);
        }
        else
        {
            return false;
        }

        var cve = inner["cveRastreo"]?.Value<string>();
        if (string.IsNullOrWhiteSpace(cve))
        {
            return false;
        }

        cveRastreo = cve;
        return true;
    }
}
