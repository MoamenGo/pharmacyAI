using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaAI.Domain.Entities;

namespace PharmaAI.Infrastructure.Persistence.Configurations;

internal sealed class AppSettingConfiguration : IEntityTypeConfiguration<AppSetting>
{
    public void Configure(EntityTypeBuilder<AppSetting> builder)
    {
        builder.ToTable("AppSettings");
        builder.HasKey(s => s.Key);
        builder.Property(s => s.Key).HasMaxLength(AppSetting.KeyMaxLength);
        builder.Property(s => s.Value).HasMaxLength(AppSetting.ValueMaxLength).IsRequired();
        builder.Property(s => s.UpdatedAtUtc).IsRequired();
    }
}
