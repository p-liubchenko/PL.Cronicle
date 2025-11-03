# PL.Cronicle

Lightweight helper to emit Cronicle-compatible JSON control messages without external JSON dependencies.

Features:
- Progress reporting (accepts 0-1 or 0-100)
- Completion with code, optional description and perf metrics
- Table and HTML sections for rich job output
- Set job label
- Works on .NET Standard 2.1

Quick start:

```csharp
var reporter = new PL.Cronicle.CronicleReporter();
reporter.ReportProgress(50); // -> {"progress":0.5}
reporter.ReportComplete(0, description: "All good");
```

License: MIT