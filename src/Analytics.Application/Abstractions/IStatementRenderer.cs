using Analytics.Domain.Reports;

namespace Analytics.Application.Abstractions;

public interface IStatementRenderer
{
    string ContentType { get; }

    byte[] Render(MonthlyStatement statement);
}
