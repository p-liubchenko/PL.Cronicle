using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace PL.Cronicle
{
    /// <summary>
    /// Minimal helper to emit Cronicle-compatible JSON line messages to stdout.
    /// This focuses on common fields used by Cronicle plugin wrappers such as progress, status, pid and completion.
    /// Regular stdout lines are captured as job logs by Cronicle.
    /// </summary>
    public interface ICronicleReporter
    {
        void ReportPid(int? pid = null);
        void ReportProgress(double percent);
        void ReportStatus(string status);
        void ReportLog(string message);
        void ReportError(string message);
        void ReportComplete(bool success, string? status = null, int? code = null, IReadOnlyDictionary<string, int>? perf = null);
    }

    /// <summary>
    /// Default implementation that writes JSON lines to <see cref="Console.Out"/> for control messages
    /// and raw lines for regular logs. Avoids external JSON dependencies for maximum portability on .NET Standard 2.1.
    /// </summary>
    public sealed class CronicleReporter : ICronicleReporter
    {
        private readonly TextWriter _writer;

        public CronicleReporter(TextWriter? writer = null)
        {
            _writer = writer ?? Console.Out;
        }

        public void ReportPid(int? pid = null)
        {
            pid ??= TryGetPid();
            WriteRawJsonLine("{\"pid\":" + pid + "}");
        }

        public void ReportProgress(double percent)
        {
            if (double.IsNaN(percent) || double.IsInfinity(percent)) percent = 0;
            if (percent < 0) percent = 0;
            if (percent > 100) percent = 100;
            WriteRawJsonLine("{\"progress\":" + percent.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}");
        }

        public void ReportStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status)) return;
            WriteRawJsonLine("{\"status\":\"" + Escape(status) + "\"}");
        }

        public void ReportLog(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            // Cronicle captures plain stdout as job logs. No special JSON needed here.
            _writer.WriteLine(message);
            _writer.Flush();
        }

        public void ReportError(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            WriteRawJsonLine("{\"error\":\"" + Escape(message) + "\"}");
        }

        public void ReportComplete(bool success, string? status = null, int? code = null, IReadOnlyDictionary<string, int>? perf = null)
        {
            var sb = new StringBuilder();
            sb.Append("{\"complete\":1");
            sb.Append(",\"success\":");
            sb.Append(success ? "true" : "false");
            if (!string.IsNullOrEmpty(status))
            {
                sb.Append(",\"status\":\"");
                sb.Append(Escape(status!));
                sb.Append("\"");
            }
            if (code.HasValue)
            {
                sb.Append(",\"code\":");
                sb.Append(code.Value);
            }
            if (perf != null && perf.Count > 0)
            {
                AppendPerf(sb, perf);
            }
            sb.Append("}");
            WriteRawJsonLine(sb.ToString());
        }

        private static void AppendPerf(StringBuilder sb, IReadOnlyDictionary<string, int> perf)
        {
            sb.Append(",\"perf\":{");
            var first = true;
            foreach (var kv in perf)
            {
                if (string.IsNullOrEmpty(kv.Key)) continue;
                if (!first) sb.Append(',');
                first = false;
                sb.Append('"');
                sb.Append(Escape(kv.Key));
                sb.Append("":");
                sb.Append(kv.Value);
            }
            sb.Append('}');
        }

        private static int TryGetPid()
        {
            try { return Process.GetCurrentProcess().Id; }
            catch { return -1; }
        }

        private void WriteRawJsonLine(string json)
        {
            _writer.WriteLine(json);
            _writer.Flush();
        }

        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var sb = new StringBuilder(s.Length + 16);
            foreach (var ch in s)
            {
                switch (ch)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (ch < ' ')
                        {
                            sb.Append("\\u");
                            sb.Append(((int)ch).ToString("x4"));
                        }
                        else
                        {
                            sb.Append(ch);
                        }
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
