using Analytics.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Analytics.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox");

        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(message => message.RoutingKey).HasColumnName("routing_key").HasMaxLength(100).IsRequired();
        builder.Property(message => message.Type).HasColumnName("type").HasMaxLength(100).IsRequired();
        builder.Property(message => message.SchemaVersion).HasColumnName("schema_version");
        builder.Property(message => message.CorrelationId).HasColumnName("correlation_id").HasMaxLength(100).IsRequired();
        builder.Property(message => message.TraceParent).HasColumnName("trace_parent").HasMaxLength(55);
        builder.Property(message => message.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(message => message.CreatedAt).HasColumnName("created_at");
        builder.Property(message => message.PublishedAt).HasColumnName("published_at");

        // Der Dispatcher sucht nur, was noch nicht publiziert ist; der Teilindex bleibt klein.
        builder.HasIndex(message => message.CreatedAt)
            .HasDatabaseName("ix_outbox_pending")
            .HasFilter("published_at IS NULL");
    }
}
