using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Analytics.Infrastructure.Persistence.Configurations;

public sealed class OwnerBookingConfiguration : IEntityTypeConfiguration<OwnerBooking>
{
    public void Configure(EntityTypeBuilder<OwnerBooking> builder)
    {
        builder.ToTable("owner_bookings");

        builder.HasKey(booking => booking.TransactionId);

        builder.Property(booking => booking.TransactionId)
            .HasColumnName("transaction_id")
            .ValueGeneratedNever();

        builder.Property(booking => booking.OwnerId)
            .HasColumnName("owner_id")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(booking => booking.Kind)
            .HasColumnName("kind")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(booking => booking.Amount)
            .HasColumnName("amount")
            .HasColumnType("numeric(19,2)")
            .IsRequired();

        builder.Property(booking => booking.Currency)
            .HasColumnName("currency")
            .HasColumnType("char(3)")
            .IsRequired();

        builder.Property(booking => booking.BookedAt)
            .HasColumnName("booked_at")
            .IsRequired();

        // Das Protokoll wird immer je Kunde und neueste zuerst gelesen.
        builder.HasIndex(booking => new { booking.OwnerId, booking.BookedAt })
            .HasDatabaseName("ix_owner_bookings_owner_booked_at")
            .IsDescending(false, true);
    }
}
