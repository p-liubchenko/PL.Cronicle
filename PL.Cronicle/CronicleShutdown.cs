using System;
using System.Threading;
using System.Threading.Tasks;

namespace PL.Cronicle
{
    /// <summary>
    /// Provides a CancellationToken that is canceled when the process is shutting down
    /// (Ctrl+C, SIGTERM or ProcessExit). Useful for Cronicle's shutdown handling.
    /// </summary>
    public static class CronicleShutdown
    {
        public static CancellationToken Token => _cts.Token;
        private static readonly CancellationTokenSource _cts = CreateLinkedCts();

        private static CancellationTokenSource CreateLinkedCts()
        {
            var cts = new CancellationTokenSource();

            void cancel()
            {
                try { cts.Cancel(); } catch { /* ignore */ }
            }

            AppDomain.CurrentDomain.ProcessExit += (_, __) => cancel();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true; // allow graceful shutdown
                cancel();
            };

            return cts;
        }

        public static Task WaitAsync() => WaitAsync(Token);

        public static async Task WaitAsync(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<object?>();
            using (cancellationToken.Register(() => tcs.TrySetResult(null)))
            {
                await tcs.Task.ConfigureAwait(false);
            }
        }
    }
}
