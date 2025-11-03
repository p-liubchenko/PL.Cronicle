namespace PL.Cronicle.Tests;

public class CronicleTaskRunnerTests
{
	[Fact]
	public async Task RunAsync_Success_WritesComplete()
	{
		var sw = new StringWriter();
		var reporter = new PL.Cronicle.CronicleReporter(sw);

		var exit = await PL.Cronicle.CronicleTaskRunner.RunAsync(async (r, ct) =>
		{
			r.ReportLog("work...");
			await Task.Delay(10, ct);
		}, reporter);

		Assert.Equal(0, exit);
		var lines = sw.ToString().Trim().Split(Environment.NewLine);
		Assert.EndsWith("{\"progress\":1}", lines[^3]);
		Assert.Equal("{\"complete\":1,\"code\":0}", lines[^1]);
	}

	[Fact]
	public async Task RunAsync_Canceled_WritesCode130()
	{
		var sw = new StringWriter();
		var reporter = new PL.Cronicle.CronicleReporter(sw);
		using var cts = new CancellationTokenSource();

		var task = PL.Cronicle.CronicleTaskRunner.RunAsync(async (r, ct) =>
		{
			cts.Cancel();
			await Task.Delay(100, ct);
		}, reporter, cts.Token);

		var exit = await task;
		Assert.Equal(130, exit);
		Assert.Contains("\"code\":130", sw.ToString().Trim());
	}

	[Fact(Skip = "Skipping for now")]
	public async Task RunAsync_Error_WritesCode1()
	{
		var sw = new StringWriter();
		var reporter = new PL.Cronicle.CronicleReporter(sw);

		var exit = await Assert.ThrowsAsync<Exception>(async () =>
		{
			// We want runner to catch and not throw. So run and assert exit code and output instead.
			await Task.CompletedTask;
		})!;
	}
}