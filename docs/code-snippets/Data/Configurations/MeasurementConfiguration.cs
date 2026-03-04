using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServerService.Models;

namespace ServerService.Data.Configurations
{
    public class MeasurementConfiguration : IEntityTypeConfiguration<Measurement>
    {
        public void Configure(EntityTypeBuilder<Measurement> builder)
        {
            builder.HasKey(m => m.Id);

            builder.Property(m => m.Alpha)
                .IsRequired();

            builder.Property(m => m.Beta)
                .IsRequired();

            builder.Property(m => m.OffsetX)
                .IsRequired();

            builder.Property(m => m.OffsetY)
                .IsRequired();

            builder.Property(m => m.CellId)
                .IsRequired();

            builder.Property(m => m.UserId)
                .IsRequired();

            builder.HasOne(m => m.Cell)
                .WithMany(c => c.Measurements)
                .HasForeignKey(m => m.CellId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(m => m.User)
                .WithMany(u => u.Measurements)
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}