using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace WorkerRetryOperationsASP.Data;

public sealed class WebhookItravelStpRepository(IConfiguration configuration) : IWebhookItravelStpRepository
{
    private readonly string _connectionString = string.IsNullOrWhiteSpace(configuration.GetConnectionString("AspDb"))
        ? throw new InvalidOperationException("Falta configurar ConnectionStrings:AspDb.")
        : configuration.GetConnectionString("AspDb")!;

    public async Task<bool> ExistsAsync(string requestId, CancellationToken ct)
    {
        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand(
            "SELECT TOP 1 1 FROM dbo.webhooks_itravel_stp WHERE RequestId = @RequestId", connection);
        command.Parameters.Add(new SqlParameter("@RequestId", SqlDbType.VarChar, 255) { Value = requestId });

        await connection.OpenAsync(ct);
        var result = await command.ExecuteScalarAsync(ct);
        return result is not null;
    }
}
