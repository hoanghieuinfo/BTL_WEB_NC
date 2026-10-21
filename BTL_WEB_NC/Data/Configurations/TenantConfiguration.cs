// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BTL_WEB_NC.Data.Configurations;

/// <summary>Bảng 3.10.</summary>
public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> b)
    {
        b.ToTable("Tenants");

        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(60).IsUnicode(false).IsRequired();
        b.HasIndex(x => x.Slug).IsUnique();
        b.Property(x => x.Phone).HasMaxLength(15).IsUnicode(false);
        b.Property(x => x.LockReason).HasMaxLength(255);

        b.HasOne(x => x.ServicePlan)
            .WithMany(p => p.Tenants)
            .HasForeignKey(x => x.ServicePlanId);
    }
}
