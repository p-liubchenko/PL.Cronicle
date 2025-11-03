using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Data;
using System.Globalization;

namespace PL.Cronicle
{
    /// <summary>
    /// Minimal helper to emit Cronicle-compatible JSON line messages to stdout.
    /// Focuses on documented Cronicle fields: progress (0.0 - 1.0), completion with code/description,
    /// optional perf metrics, and a few convenience log methods.
    /// Plain stdout/stderr lines are captured as job logs by Cronicle.
    /// </summary>
    public interface ICronicleReporter
    {
        void ReportPid(int? pid = null);
        void ReportProgress(double value);
        void ReportStatus(string status);
        void ReportLog(string message);
        void ReportError(string message);
        void ReportComplete(int code, string? description = null, IReadOnlyDictionary<string, double>? perf = null, double? perfScale = null);

        // Custom Data Tables (Cronicle "table" section)
        void ReportTable(string? title, IEnumerable<string>? header, IEnumerable<IEnumerable<object?>> rows, string? caption = null);
        void ReportTableFromDataTable(DataTable table, string? title = null, string? caption = null, bool includeColumnNamesAsHeader = true);

        // Custom HTML Content (Cronicle "html" section)
        void ReportHtml(string content, string? title = null, string? caption = null);

        // Custom Job Label
        void SetLabel(string label);
        // Alias with common misspelling for convenience
        void SetLable(string label);
    }

    public enum CronicleStatus
    {
        Starting,
        Running,
        Complete,
        Canceled,
        Error
    }

    /// <summary>
    /// Default implementation that writes JSON lines to <see cref="Console.Out"/> for control messages
    /// and raw lines for regular logs. Avoids external JSON dependencies for maximum portability on .NET Standard 2.1.
    /// </summary>
    public sealed class CronicleReporter : ICronicleReporter
    {
        private readonly TextWriter _writer;
        private readonly object _sync = new object();

        public CronicleReporter(TextWriter? writer = null)
        {
            _writer = writer ?? Console.Out;
        }

        public void ReportPid(int? pid = null)
        {
            pid ??= TryGetPid();
            // Not a documented JSON control field; emit as plain log for visibility
            lock (_sync)
            {
                _writer.WriteLine("PID: " + pid);
                _writer.Flush();
            }
        }

        public void ReportProgress(double value)
        {
            // Cronicle expects 0.0 - 1.0 in JSON. Accept 0-100 (whole numbers) and convert; otherwise clamp ratio.
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                value = 0d;
            }
            else
            {
                const double eps = 1e-9;
                // Treat as percent only when clearly above 1 and looks like a whole number (e.g., 50 -> 0.5)
                bool looksWhole = Math.Abs(value - Math.Round(value)) <= eps;
                if (value > 1.0 + eps && looksWhole)
                {
                    value = value / 100.0;
                }
            }

            // Clamp to [0,1] without additional comparisons to 1.0
            value = Math.Max(0d, Math.Min(1d, value));

            var json = "{\"progress\":" + value.ToString(CultureInfo.InvariantCulture) + "}";
            WriteRawJsonLine(json);
        }

        public void ReportStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status)) return;
            // Not a documented JSON control field; emit as plain log
            lock (_sync)
            {
                _writer.WriteLine(status);
                _writer.Flush();
            }
        }

        public void ReportStatus(CronicleStatus status)
        {
            ReportStatus(status.ToString());
        }

        public void ReportLog(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            // Cronicle captures plain stdout as job logs. No special JSON needed here.
            lock (_sync)
            {
                _writer.WriteLine(message);
                _writer.Flush();
            }
        }

        public void ReportError(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            // Send to STDERR and STDOUT so it always appears in logs
            try { Console.Error.WriteLine(message); Console.Error.Flush(); } catch { /* ignore */ }
            lock (_sync)
            {
                _writer.WriteLine(message);
                _writer.Flush();
            }
        }

        public void ReportComplete(int code, string? description = null, IReadOnlyDictionary<string, double>? perf = null, double? perfScale = null)
        {
            var sb = new StringBuilder();
            sb.Append("{\"complete\":1");
            sb.Append(",\"code\":");
            sb.Append(code);
            if (!string.IsNullOrEmpty(description))
            {
                sb.Append(",\"description\":\"");
                sb.Append(Escape(description!));
                sb.Append("\"");
            }
            if (perf != null && perf.Count > 0)
            {
                AppendPerf(sb, perf, perfScale);
            }
            sb.Append("}");
            WriteRawJsonLine(sb.ToString());
        }

        public void ReportTable(string? title, IEnumerable<string>? header, IEnumerable<IEnumerable<object?>> rows, string? caption = null)
        {
            var sb = new StringBuilder();
            sb.Append("{\"table\":{");
            var firstProp = true;

            if (!string.IsNullOrEmpty(title))
            {
                AppendCommaIfNeeded(sb, ref firstProp);
                sb.Append("\"title\":\"");
                sb.Append(Escape(title!));
                sb.Append("\"");
            }

            if (header != null)
            {
                var headerList = ToList(header);
                if (headerList.Count > 0)
                {
                    AppendCommaIfNeeded(sb, ref firstProp);
                    sb.Append("\"header\":[");
                    for (int i = 0; i < headerList.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        sb.Append('"');
                        sb.Append(Escape(headerList[i] ?? string.Empty));
                        sb.Append('"');
                    }
                    sb.Append(']');
                }
            }

            AppendCommaIfNeeded(sb, ref firstProp);
            sb.Append("\"rows\":[");
            var firstRow = true;
            foreach (var row in rows)
            {
                if (!firstRow) sb.Append(',');
                firstRow = false;
                sb.Append('[');
                var firstCell = true;
                foreach (var cell in row)
                {
                    if (!firstCell) sb.Append(',');
                    firstCell = false;
                    WriteJsonValue(sb, cell);
                }
                sb.Append(']');
            }
            sb.Append(']');

            if (!string.IsNullOrEmpty(caption))
            {
                AppendCommaIfNeeded(sb, ref firstProp);
                sb.Append("\"caption\":\"");
                sb.Append(Escape(caption!));
                sb.Append("\"");
            }

            sb.Append("}}");
            WriteRawJsonLine(sb.ToString());
        }

        public void ReportTableFromDataTable(DataTable table, string? title = null, string? caption = null, bool includeColumnNamesAsHeader = true)
        {
            var header = includeColumnNamesAsHeader ? GetColumnNames(table) : null;
            IEnumerable<IEnumerable<object?>> rows = EnumerateRows(table);
            ReportTable(title, header, rows, caption);
        }

        public void ReportHtml(string content, string? title = null, string? caption = null)
        {
            if (string.IsNullOrEmpty(content)) return; // content is required by Cronicle
            var sb = new StringBuilder();
            sb.Append("{\"html\":{");
            var firstProp = true;
            if (!string.IsNullOrEmpty(title))
            {
                AppendCommaIfNeeded(sb, ref firstProp);
                sb.Append("\"title\":\"");
                sb.Append(Escape(title!));
                sb.Append("\"");
            }
            AppendCommaIfNeeded(sb, ref firstProp);
            sb.Append("\"content\":\"");
            sb.Append(Escape(content));
            sb.Append("\"");
            if (!string.IsNullOrEmpty(caption))
            {
                AppendCommaIfNeeded(sb, ref firstProp);
                sb.Append("\"caption\":\"");
                sb.Append(Escape(caption!));
                sb.Append("\"");
            }
            sb.Append("}}");
            WriteRawJsonLine(sb.ToString());
        }

        public void SetLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label)) return;
            WriteRawJsonLine("{\"label\":\"" + Escape(label) + "\"}");
        }

        public void SetLable(string label) => SetLabel(label);

        private static IReadOnlyList<string> GetColumnNames(DataTable table)
        {
            var list = new List<string>(table.Columns.Count);
            foreach (DataColumn col in table.Columns)
            {
                list.Add(col.ColumnName);
            }
            return list;
        }

        private static IEnumerable<IEnumerable<object?>> EnumerateRows(DataTable table)
        {
            foreach (DataRow row in table.Rows)
            {
                var arr = new object?[table.Columns.Count];
                for (int i = 0; i < table.Columns.Count; i++) arr[i] = row[i];
                yield return arr;
            }
        }

        private static void AppendPerf(StringBuilder sb, IReadOnlyDictionary<string, double> perf, double? perfScale)
        {
            sb.Append(",\"perf\":{");
            var first = true;
            if (perfScale.HasValue)
            {
                sb.Append("\"scale\":");
                sb.Append(perfScale.Value.ToString(CultureInfo.InvariantCulture));
                first = false;
            }
            foreach (var kv in perf)
            {
                if (string.IsNullOrEmpty(kv.Key)) continue;
                if (!first) sb.Append(',');
                first = false;
                sb.Append('"');
                sb.Append(Escape(kv.Key));
                sb.Append("\":");
                sb.Append(kv.Value.ToString(CultureInfo.InvariantCulture));
            }
            sb.Append('}');
        }

        private static void WriteJsonValue(StringBuilder sb, object? value)
        {
            if (value == null || value == DBNull.Value)
            {
                sb.Append("null");
                return;
            }
            switch (value)
            {
                case string s:
                    sb.Append('"'); sb.Append(Escape(s)); sb.Append('"');
                    break;
                case bool b:
                    sb.Append(b ? "true" : "false");
                    break;
                case byte _:
                case sbyte _:
                case short _:
                case ushort _:
                case int _:
                case uint _:
                case long _:
                case ulong _:
                case float _:
                case double _:
                case decimal _:
                    sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                    break;
                case DateTime dt:
                    sb.Append('"'); sb.Append(dt.ToString("o", CultureInfo.InvariantCulture)); sb.Append('"');
                    break;
                case DateTimeOffset dto:
                    sb.Append('"'); sb.Append(dto.ToString("o", CultureInfo.InvariantCulture)); sb.Append('"');
                    break;
                default:
                    sb.Append('"'); sb.Append(Escape(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty)); sb.Append('"');
                    break;
            }
        }

        private static void AppendCommaIfNeeded(StringBuilder sb, ref bool first)
        {
            if (!first) sb.Append(',');
            first = false;
        }

        private static int TryGetPid()
        {
            try { return Process.GetCurrentProcess().Id; }
            catch { return -1; }
        }

        private void WriteRawJsonLine(string json)
        {
            lock (_sync)
            {
                _writer.WriteLine(json);
                _writer.Flush();
            }
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

        private static List<string> ToList(IEnumerable<string> items)
        {
            var list = new List<string>();
            foreach (var it in items) list.Add(it);
            return list;
        }
    }
}
