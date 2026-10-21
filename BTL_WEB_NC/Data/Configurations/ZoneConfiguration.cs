// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BTL_WEB_NC.Data.Configurations;

/// <summary>Bảng 3.13 – Zones.</summary>
public class ZoneConfiguration : IEntityTypeConfiguration<Zone>
{
    public void Configure(EntityTypeBuilder<Zone> b)
    {
        b.ToTable("Zones");

        b.Property(x => x.Name).HasMaxLength(50).IsRequired();

        b.HasOne(x => x.Branch).WithMany(br => br.Zones).HasForeignKey(x => x.BranchId);

        b.HasIndex(x => new { x.BranchId, x.Name }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.BranchId });
    }
}
