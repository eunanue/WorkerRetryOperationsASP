using WorkerRetryOperationsASP.Models;

namespace WorkerRetryOperationsASP.Correlation;

public interface ICorrelationService
{
    IReadOnlyList<PendingRetryItem> Correlate(IReadOnlyList<LogSuccessRecord> successes, IReadOnlyList<LogErrorRecord> errors);
}
