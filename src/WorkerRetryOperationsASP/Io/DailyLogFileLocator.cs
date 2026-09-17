using Microsoft.Extensions.Options;
using WorkerRetryOperationsASP.Configuration;

namespace WorkerRetryOperationsASP.Io;

public sealed class DailyLogFileLocator(IOptions<LogSourceOptions> options) : ILogFileLocator
{
    private readonly LogSourceOptions _options = options.Value;

    public string? GetTodayLogFilePath()
    {
        var fileName = $"{_options.FileNamePrefix}{DateTime.Now:yyyy-MM-dd}{_options.FileNameExtension}";
        var fullPath = Path.Combine(_options.FolderPath, fileName);
        return File.Exists(fullPath) ? fullPath : null;
    }
}
