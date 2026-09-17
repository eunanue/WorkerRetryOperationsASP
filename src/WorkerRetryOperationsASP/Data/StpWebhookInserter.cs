using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WorkerRetryOperationsASP.Configuration;

namespace WorkerRetryOperationsASP.Data;

/// <summary>
/// Reproduce exactamente la llamada a dbo.usp_WebhookItravel_InsertASP que hace
/// el middleware original (método ProcessSTPSPWebhook), para reintentar inserts
/// que fallaron por timeout.
/// </summary>
public sealed class StpWebhookInserter(
    IConfiguration configuration,
    IOptions<RetryWorkerOptions> options,
    ILogger<StpWebhookInserter> logger) : IStpWebhookInserter
{
    private readonly string _connectionString = string.IsNullOrWhiteSpace(configuration.GetConnectionString("AspDb"))
        ? throw new InvalidOperationException("Falta configurar ConnectionStrings:AspDb.")
        : configuration.GetConnectionString("AspDb")!;
    private readonly RetryWorkerOptions _options = options.Value;

    public async Task<bool> TryInsertAsync(string requestId, string eventType, string payloadJson, CancellationToken ct)
    {
        try
        {
            var parameters = new[]
            {
                new SqlParameter("@RequestId", SqlDbType.VarChar, 255)
                {
                    Value = string.IsNullOrWhiteSpace(requestId) ? DBNull.Value : requestId
                },
                new SqlParameter("@EventType", SqlDbType.NVarChar, 100)
                {
                    Value = eventType
                },
                new SqlParameter("@Payload", SqlDbType.NVarChar, -1)
                {
                    Value = payloadJson
                },
                new SqlParameter("@Status", SqlDbType.NVarChar, 100)
                {
                    Value = "received"
                }
            };

            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("dbo.usp_WebhookItravel_InsertASP", connection)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = _options.SqlCommandTimeoutSeconds
            };
            command.Parameters.AddRange(parameters);

            await connection.OpenAsync(ct);
            await command.ExecuteNonQueryAsync(ct);
            return true;
        }
        catch (SqlException ex)
        {
            logger.LogWarning(ex, "Reintento fallido (SqlException) para RequestId={RequestId} EventType={EventType}.", requestId, eventType);
            return false;
        }
    }
}
