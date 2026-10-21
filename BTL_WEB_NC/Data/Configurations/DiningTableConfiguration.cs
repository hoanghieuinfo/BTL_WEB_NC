// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BTL_WEB_NC.Data.Configurations;

/// <summary>Bảng 3.13 – DiningTables.</summary>
public class DiningTableConfiguration : IEntityTypeConfiguration<DiningTable>
{
    public void Configure(EntityTypeBuilder<DiningTable> b)
    {
        b.ToTable("DiningTables");

        b.Property(x => x.Name).HasMaxLength(20).IsRequired();

        b.HasOne(x => x.Zone).WithMany(z => z.Tables).HasForeignKey(x => x.ZoneId);

        b.HasIndex(x => new { x.ZoneId, x.Name }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.ZoneId });
    }
}
