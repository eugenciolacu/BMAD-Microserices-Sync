using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sync.Domain.Entities;

namespace Sync.Infrastructure.Data.Configurations;

public class CellConfiguration : IEntityTypeConfiguration<Cell>
{
    public void Configure(EntityTypeBuilder<Cell> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Identifier)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(c => c.Identifier)
            .IsUnique();

        builder.Property(c => c.SurfaceId)
            .IsRequired();

        builder.HasMany(c => c.Measurements)
            .WithOne(m => m.Cell)
            .HasForeignKey(m => m.CellId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
