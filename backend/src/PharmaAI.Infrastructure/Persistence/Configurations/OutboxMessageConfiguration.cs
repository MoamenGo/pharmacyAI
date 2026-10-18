using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaAI.Domain.Sync;

namespace PharmaAI.Infrastructure.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(m => m.Id);
        // Ids are created in the domain; never let EF guess "existing" from a set key.
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Kind).HasMaxLength(50).IsRequired();
        builder.Property(m => m.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(m => m.LastError).HasMaxLength(OutboxMessage.ErrorMaxLength);
        builder.Property(m => m.ExternalId).HasMaxLength(100);
        builder.Property(m => m.PreviousExternalId).HasMaxLength(100);
        builder.Property(m => m.FirstExternalId).HasMaxLength(100);
        builder.Property(m => m.ReplacedExternalId).HasMaxLength(100);

        // One message per subject and kind: a sale is never queued twice.
        builder.HasIndex(m => new { m.Kind, m.SubjectId }).IsUnique();
        builder.HasIndex(m => new { m.Kind, m.Sequence }).IsUnique();
        builder.HasIndex(m => new { m.Status, m.NextAttemptAtUtc });
    }
}
