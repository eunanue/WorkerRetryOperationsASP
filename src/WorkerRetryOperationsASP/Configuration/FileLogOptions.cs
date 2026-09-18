namespace WorkerRetryOperationsASP.Configuration;

public sealed class FileLogOptions
{
    /// <summary>
    /// Carpeta donde el worker escribe su propio log. Si es relativa se resuelve
    /// contra la carpeta del ejecutable (no contra el directorio de trabajo, que
    /// para un Servicio de Windows es System32).
    /// </summary>
    public string FolderPath { get; set; } = "logs";

    /// <summary>Prefijo del archivo; Serilog agrega la fecha: {prefijo}{yyyyMMdd}.log.</summary>
    public string FileNamePrefix { get; set; } = "WorkerRetryOperationsASP_";

    /// <summary>Cantidad de archivos diarios que se conservan; los más antiguos se borran.</summary>
    public int RetainedFileCountLimit { get; set; } = 30;
}
