using System;
using System.Threading;
using System.Threading.Tasks;

namespace PL.Cronicle
{
    /// <summary>
    /// A simple wrapper to run an async task with Cronicle-friendly reporting and shutdown handling.
    /// </summary>
    public static class CronicleTaskRunner
    {
        public static async Task<int> RunAsync(Func<ICronicleReporter, CancellationToken, Task> work, ICronicleReporter? reporter = null, CancellationToken cancellationToken = default)
        {
            reporter ??= new CronicleReporter();

            try
            {
                reporter.ReportPid();
                reporter.ReportStatus("starting");

                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CronicleShutdown.Token, cancellationToken);
                await work(reporter, linkedCts.Token).ConfigureAwait(false);

                reporter.ReportProgress(1.0);
                reporter.ReportStatus("complete");
                reporter.ReportComplete(code: 0);
                return 0;
            }
            catch (OperationCanceledException)
            {
                reporter.ReportStatus("canceled");
                reporter.ReportError("Operation canceled");
                reporter.ReportComplete(code: 130, description: "Operation canceled");
                return 130; // common termination code for SIGINT
            }
            catch (Exception ex)
            {
                reporter.ReportStatus("error");
                reporter.ReportError(ex.Message);
                reporter.ReportComplete(code: 1, description: ex.Message);
                return 1;
            }
        }
    }
}
