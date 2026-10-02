using Analytics.Application.Abstractions;
using Analytics.Domain.Reports;
using Microsoft.EntityFrameworkCore;

namespace Analytics.Infrastructure.Persistence;

public sealed class MonthlyReportRepository : IMonthlyReportRepository
{
    private readonly AnalyticsDbContext _context;

    public MonthlyReportRepository(AnalyticsDbContext context)
    {
        _context = context;
    }

    public void Add(MonthlyReport report)
    {
        _context.MonthlyReports.Add(report);
    }

    public Task<MonthlyReport?> FindAsync(Guid reportId, CancellationToken cancellationToken)
    {
        return _context.MonthlyReports.SingleOrDefaultAsync(report => report.Id == reportId, cancellationToken);
    }

    public async Task<IReadOnlyList<MonthlyReport>> ListForOwnerAsync(string ownerId, int limit, CancellationToken cancellationToken)
    {
        return await _context.MonthlyReports
            .AsNoTracking()
            .Where(report => report.OwnerId == ownerId)
            .OrderByDescending(report => report.RequestedAt)
            .ThenBy(report => report.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public void AddDocument(Guid reportId, byte[] content)
    {
        _context.ReportDocuments.Add(new ReportDocumentRecord { ReportId = reportId, Content = content });
    }

    public Task<byte[]?> FindDocumentAsync(Guid reportId, CancellationToken cancellationToken)
    {
        return _context.ReportDocuments
            .Where(document => document.ReportId == reportId)
            .Select(document => document.Content)
            .SingleOrDefaultAsync(cancellationToken);
    }
}
