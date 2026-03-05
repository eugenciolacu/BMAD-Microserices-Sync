using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sync.Domain.Entities;

namespace Sync.Infrastructure.Data.Configurations;

public class SurfaceConfiguration : IEntityTypeConfiguration<Surface>
{
    public void Configure(EntityTypeBuilder<Surface> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Identifier)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(s => s.Identifier)
            .IsUnique();

        builder.Property(s => s.RoomId)
            .IsRequired();

        builder.HasMany(s => s.Cells)
            .WithOne(c => c.Surface)
            .HasForeignKey(c => c.SurfaceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
