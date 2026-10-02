using Analytics.Domain.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Analytics.Infrastructure.Persistence.Configurations;

public sealed class ReportDocumentConfiguration : IEntityTypeConfiguration<ReportDocumentRecord>
{
    public void Configure(EntityTypeBuilder<ReportDocumentRecord> builder)
    {
        builder.ToTable("report_documents");

        builder.HasKey(document => document.ReportId);
        builder.Property(document => document.ReportId).HasColumnName("report_id").ValueGeneratedNever();
        builder.Property(document => document.Content).HasColumnName("content").IsRequired();

        builder.HasOne<MonthlyReport>()
            .WithOne()
            .HasForeignKey<ReportDocumentRecord>(document => document.ReportId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
