using Analytics.Domain.Reports;

namespace Analytics.Application.Abstractions;

public interface IMonthlyReportRepository
{
    void Add(MonthlyReport report);

    Task<MonthlyReport?> FindAsync(Guid reportId, CancellationToken cancellationToken);

    // Die Berichte eines Inhabers, neueste zuerst.
    Task<IReadOnlyList<MonthlyReport>> ListForOwnerAsync(string ownerId, int limit, CancellationToken cancellationToken);

    void AddDocument(Guid reportId, byte[] content);

    Task<byte[]?> FindDocumentAsync(Guid reportId, CancellationToken cancellationToken);
}
