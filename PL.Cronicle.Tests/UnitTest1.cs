using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PL.Cronicle.Tests;

public class CronicleReporterTests
{
    [Fact]
    public void ReportProgress_WritesJson_ClampsAndConverts()
    {
        var sw = new StringWriter();
        var reporter = new PL.Cronicle.CronicleReporter(sw);

        reporter.ReportProgress(50); // interpret 50 as 0.5
        reporter.ReportProgress(1.5); // clamp to 1.0
        reporter.ReportProgress(-1); // clamp to 0
        reporter.ReportProgress(25); // clamp to 0

        var lines = sw.ToString().Trim().Split(Environment.NewLine);
        Assert.Equal("{\"progress\":0.5}", lines[0]);
        Assert.Equal("{\"progress\":1}", lines[1]);
        Assert.Equal("{\"progress\":0}", lines[2]);
        Assert.Equal("{\"progress\":0.25}", lines[3]);
    }

    [Fact]
    public void ReportComplete_Success_NoPerf()
    {
        var sw = new StringWriter();
        var reporter = new PL.Cronicle.CronicleReporter(sw);

        reporter.ReportComplete(code: 0);
        var line = sw.ToString().Trim();
        Assert.Equal("{\"complete\":1,\"code\":0}", line);
    }

    [Fact]
    public void ReportComplete_WithDescription_And_Perf_WithScale()
    {
        var sw = new StringWriter();
        var reporter = new PL.Cronicle.CronicleReporter(sw);

        reporter.ReportComplete(0, description: "All good", perf: new Dictionary<string, double> { ["db"] = 1.23, ["http"] = 2 }, perfScale: 1000);
        var line = sw.ToString().Trim();
        Assert.Equal("{\"complete\":1,\"code\":0,\"description\":\"All good\",\"perf\":{\"scale\":1000,\"db\":1.23,\"http\":2}}", line);
    }

    [Fact]
    public void ReportTable_And_ReportHtml_And_Label()
    {
        var sw = new StringWriter();
        var reporter = new PL.Cronicle.CronicleReporter(sw);

        reporter.ReportTable(
            title: "Sample",
            header: new[] { "Col1", "Col2" },
            rows: new[] { new object?[] { 1, "x" }, new object?[] { null, true } },
            caption: "Done");

        reporter.ReportHtml("<b>Hi</b>", title: "T", caption: "C");
        reporter.SetLabel("Job ABC");

        var lines = sw.ToString().Trim().Split(Environment.NewLine);
        Assert.Equal("{\"table\":{\"title\":\"Sample\",\"header\":[\"Col1\",\"Col2\"],\"rows\":[[1,\"x\"],[null,true]],\"caption\":\"Done\"}}", lines[0]);
        Assert.Equal("{\"html\":{\"title\":\"T\",\"content\":\"<b>Hi</b>\",\"caption\":\"C\"}}", lines[1]);
        Assert.Equal("{\"label\":\"Job ABC\"}", lines[2]);
    }

    [Fact]
    public void ReportTable_From_DataTable()
    {
        var table = new DataTable();
        table.Columns.Add("A", typeof(int));
        table.Columns.Add("B", typeof(string));
        table.Rows.Add(1, "x");
        table.Rows.Add(DBNull.Value, "y");

        var sw = new StringWriter();
        var reporter = new PL.Cronicle.CronicleReporter(sw);
        reporter.ReportTableFromDataTable(table, title: "T", caption: "C");

        var line = sw.ToString().Trim();
        Assert.Equal("{\"table\":{\"title\":\"T\",\"header\":[\"A\",\"B\"],\"rows\":[[1,\"x\"],[null,\"y\"]],\"caption\":\"C\"}}", line);
    }
}
