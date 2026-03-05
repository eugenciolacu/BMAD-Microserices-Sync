using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sync.Domain.Entities;

namespace Sync.Infrastructure.Data.Configurations;

public class BuildingConfiguration : IEntityTypeConfiguration<Building>
{
    public void Configure(EntityTypeBuilder<Building> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Identifier)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(b => b.Identifier)
            .IsUnique();

        builder.HasMany(b => b.Rooms)
            .WithOne(r => r.Building)
            .HasForeignKey(r => r.BuildingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
