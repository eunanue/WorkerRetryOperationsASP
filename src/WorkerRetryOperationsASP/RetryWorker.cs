using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WorkerRetryOperationsASP.Configuration;
using WorkerRetryOperationsASP.Orchestration;

namespace WorkerRetryOperationsASP;

public sealed class RetryWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<RetryWorkerOptions> options,
    IHostApplicationLifetime lifetime,
    ILogger<RetryWorker> logger) : BackgroundService
{
    private readonly RetryWorkerOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.DryRun)
        {
            logger.LogInformation("Modo DRY RUN activo: solo se listarán los candidatos a reintento, no se llamará a SQL Server. El proceso termina al finalizar.");
            using var dryRunScope = scopeFactory.CreateScope();
            var reporter = dryRunScope.ServiceProvider.GetRequiredService<IDryRunReporter>();
            await reporter.RunOnceAsync(stoppingToken);
            lifetime.StopApplication();
            return;
        }

        logger.LogInformation("RetryWorker iniciado. Intervalo configurado: {IntervalHours}h.", _options.IntervalHours);

        if (_options.RunOnStartup)
        {
            await SafeRunOnceAsync(stoppingToken);
        }

        using var timer = new PeriodicTimer(TimeSpan.FromHours(_options.IntervalHours));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await SafeRunOnceAsync(stoppingToken);
        }
    }

    private async Task SafeRunOnceAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var orchestrator = scope.ServiceProvider.GetRequiredService<IRetryOrchestrator>();
            await orchestrator.RunOnceAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Apagado normal del servicio.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fallo no controlado en la corrida del RetryWorker; se reintentará en la próxima ejecución programada.");
        }
    }
}
