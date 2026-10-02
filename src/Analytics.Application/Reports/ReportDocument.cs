namespace Analytics.Application.Reports;

public sealed record ReportDocument(byte[] Content, string ContentType, string FileName);
