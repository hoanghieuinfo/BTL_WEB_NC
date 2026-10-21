// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BTL_WEB_NC.Data.Configurations;

/// <summary>Bảng 3.16 – StockReceipts.</summary>
public class StockReceiptConfiguration : IEntityTypeConfiguration<StockReceipt>
{
    public void Configure(EntityTypeBuilder<StockReceipt> b)
    {
        b.ToTable("StockReceipts", t =>
            t.HasCheckConstraint("CK_StockReceipts_TotalAmount", "[TotalAmount] >= 0"));

        b.Property(x => x.Code).HasMaxLength(20).IsUnicode(false).IsRequired();
        b.Property(x => x.Supplier).HasMaxLength(100);
        b.Property(x => x.CreatedById).HasMaxLength(450).IsRequired();
        b.Property(x => x.TotalAmount).HasPrecision(18, 0);

        b.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId);
        b.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById);

        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.BranchId, x.ReceivedDate });
    }
}
