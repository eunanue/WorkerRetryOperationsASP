using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace WorkerRetryOperationsASP.Data;

public sealed class WebhookRetryRepository(IConfiguration configuration) : IWebhookRetryRepository
{
    private readonly string _connectionString = string.IsNullOrWhiteSpace(configuration.GetConnectionString("AspDb"))
        ? throw new InvalidOperationException("Falta configurar ConnectionStrings:AspDb.")
        : configuration.GetConnectionString("AspDb")!;

    public async Task<bool> ExistsAsync(string cveRastreo, CancellationToken ct)
    {
        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand(
            "SELECT 1 FROM dbo.reintentos_asp WHERE cve_rastreo = @cve_rastreo", connection);
        command.Parameters.Add(new SqlParameter("@cve_rastreo", SqlDbType.VarChar, 255) { Value = cveRastreo });

        await connection.OpenAsync(ct);
        var result = await command.ExecuteScalarAsync(ct);
        return result is not null;
    }

    public async Task InsertAsync(string cveRastreo, CancellationToken ct)
    {
        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand(
            "INSERT INTO dbo.reintentos_asp (cve_rastreo, fecha_insercion) VALUES (@cve_rastreo, SYSDATETIME())", connection);
        command.Parameters.Add(new SqlParameter("@cve_rastreo", SqlDbType.VarChar, 255) { Value = cveRastreo });

        await connection.OpenAsync(ct);
        try
        {
            await command.ExecuteNonQueryAsync(ct);
        }
        catch (SqlException ex) when (ex.Number is 2627 or 2601)
        {
            // Violación de UNIQUE en cve_rastreo: ya fue registrado por otra corrida/proceso. No-op defensivo.
        }
    }
}
