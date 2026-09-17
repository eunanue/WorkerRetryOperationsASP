namespace WorkerRetryOperationsASP.Configuration;

public sealed class RetryWorkerOptions
{
    public double IntervalHours { get; set; } = 1;
    public bool RunOnStartup { get; set; } = true;
    public int SqlCommandTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Cuando es true, el worker solo parsea el log, correlaciona y muestra por
    /// consola/log los candidatos a reintento (cve_rastreo + parámetros que se
    /// enviarían al SP), sin ejecutar ningún insert real ni requerir conexión a
    /// SQL Server. Corre una sola vez y termina el proceso.
    /// </summary>
    public bool DryRun { get; set; } = false;
}
