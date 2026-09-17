namespace WorkerRetryOperationsASP.Configuration;

public sealed class LogSourceOptions
{
    public string FolderPath { get; set; } = string.Empty;
    public string FileNamePrefix { get; set; } = "NotifyItravel_STP.log_";
    public string FileNameExtension { get; set; } = ".log";
}
