using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sync.Domain.Entities;

namespace Sync.Infrastructure.Data.Configurations;

public class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Identifier)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(r => r.Identifier)
            .IsUnique();

        builder.Property(r => r.BuildingId)
            .IsRequired();

        builder.HasMany(r => r.Surfaces)
            .WithOne(s => s.Room)
            .HasForeignKey(s => s.RoomId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
