using WorkerRetryOperationsASP.Models;

namespace WorkerRetryOperationsASP.Parsing;

public interface ILogParserService
{
    Task<(List<LogSuccessRecord> Successes, List<LogErrorRecord> Errors)> ParseAsync(string filePath, CancellationToken ct);
}
