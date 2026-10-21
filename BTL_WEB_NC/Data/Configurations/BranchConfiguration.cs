// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BTL_WEB_NC.Data.Configurations;

/// <summary>Bảng 3.12.</summary>
public class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> b)
    {
        b.ToTable("Branches", t =>
            t.HasCheckConstraint("CK_Branches_OpenClose", "[CloseTime] > [OpenTime]"));

        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(60).IsUnicode(false).IsRequired();
        b.Property(x => x.Address).HasMaxLength(255).IsRequired();
        b.Property(x => x.Phone).HasMaxLength(15).IsUnicode(false);

        b.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.Slug }).IsUnique();
    }
}
