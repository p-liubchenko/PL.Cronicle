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

                reporter.ReportProgress(100);
                reporter.ReportStatus("complete");
                reporter.ReportComplete(success: true);
                return 0;
            }
            catch (OperationCanceledException)
            {
                reporter.ReportStatus("canceled");
                reporter.ReportError("Operation canceled");
                reporter.ReportComplete(success: false, status: "canceled", code: 130);
                return 130; // common termination code for SIGINT
            }
            catch (Exception ex)
            {
                reporter.ReportStatus("error");
                reporter.ReportError(ex.Message);
                reporter.ReportComplete(success: false, status: ex.GetType().Name, code: 1);
                return 1;
            }
        }
    }
}
