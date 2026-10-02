using Analytics.Domain.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Analytics.Infrastructure.Persistence.Configurations;

public sealed class MonthlyReportConfiguration : IEntityTypeConfiguration<MonthlyReport>
{
    // Spalte xmin von Postgres: ändert sich bei jedem Update. Speichern zwei Instanzen denselben
    // Bericht, scheitert die zweite, statt den Zustand der ersten zu überschreiben.
    public const string VersionProperty = "Version";

    public void Configure(EntityTypeBuilder<MonthlyReport> builder)
    {
        builder.ToTable("monthly_reports");

        builder.HasKey(report => report.Id);
        builder.Property(report => report.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(report => report.OwnerId)
            .HasColumnName("owner_id")
            .HasMaxLength(100)
            .IsRequired();

        builder.Ignore(report => report.Period);
        builder.Property<int>("_year").HasColumnName("year");
        builder.Property<int>("_month").HasColumnName("month");

        builder.Property(report => report.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(report => report.BookingCount).HasColumnName("booking_count");
        builder.Property(report => report.Failure).HasColumnName("failure").HasMaxLength(500);
        builder.Property(report => report.RequestedAt).HasColumnName("requested_at");
        builder.Property(report => report.CompletedAt).HasColumnName("completed_at");
        builder.Ignore(report => report.IsCompleted);

        builder.Property<uint>(VersionProperty).IsRowVersion();

        builder.HasIndex(report => new { report.OwnerId, report.RequestedAt })
            .HasDatabaseName("ix_monthly_reports_owner_requested_at")
            .IsDescending(false, true);
    }
}
