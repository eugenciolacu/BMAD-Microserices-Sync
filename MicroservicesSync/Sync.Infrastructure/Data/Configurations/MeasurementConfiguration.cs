using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Sync.Domain.Entities;

namespace Sync.Infrastructure.Data.Configurations;

public class MeasurementConfiguration : IEntityTypeConfiguration<Measurement>
{
    public void Configure(EntityTypeBuilder<Measurement> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Value)
            .IsRequired()
            .HasPrecision(18, 4);

        // Store DateTime as UTC — ValueConverter ensures round-trip fidelity on both SQL Server and SQLite.
        var utcConverter = new ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v, DateTimeKind.Utc),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        var utcNullableConverter = new ValueConverter<DateTime?, DateTime?>(
            v => v.HasValue ? (v.Value.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc)) : v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

        builder.Property(m => m.RecordedAt)
            .IsRequired()
            .HasConversion(utcConverter);

        builder.Property(m => m.SyncedAt)
            .HasConversion(utcNullableConverter);

        builder.Property(m => m.UserId)
            .IsRequired();

        builder.Property(m => m.CellId)
            .IsRequired();

        builder.HasOne(m => m.User)
            .WithMany(u => u.Measurements)
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Cell)
            .WithMany(c => c.Measurements)
            .HasForeignKey(m => m.CellId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
