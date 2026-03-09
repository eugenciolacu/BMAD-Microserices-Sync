using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sync.Domain.Entities;

namespace Sync.Infrastructure.Data.Configurations;

public class SyncRunConfiguration : IEntityTypeConfiguration<SyncRun>
{
    public void Configure(EntityTypeBuilder<SyncRun> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RunType).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(10).IsRequired();
        builder.Property(x => x.ErrorMessage).HasMaxLength(1000);
        builder.Property(x => x.OccurredAt).IsRequired();
        builder.Property(x => x.MeasurementCount).IsRequired();

        // FK → Users; optional (pull runs have no single user identity); no cascade delete
        builder.HasOne(x => x.User)
               .WithMany()
               .HasForeignKey(x => x.UserId)
               .IsRequired(false)
               .OnDelete(DeleteBehavior.Restrict);

        // Index for the summary view sort/filter patterns (by date, user, type)
        builder.HasIndex(x => x.OccurredAt);
        builder.HasIndex(x => x.UserId);
    }
}
