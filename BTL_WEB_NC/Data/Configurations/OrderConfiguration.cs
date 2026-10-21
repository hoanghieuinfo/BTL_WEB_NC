// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BTL_WEB_NC.Data.Configurations;

/// <summary>Bảng 3.17 – Orders.</summary>
public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> b)
    {
        b.ToTable("Orders", t =>
        {
            t.HasCheckConstraint("CK_Orders_SubTotal", "[SubTotal] >= 0");
            t.HasCheckConstraint("CK_Orders_Discount", "[Discount] >= 0");
            t.HasCheckConstraint("CK_Orders_Total", "[Total] >= 0");
            t.HasCheckConstraint("CK_Orders_DiscountReason", "[Discount] = 0 OR [DiscountReason] IS NOT NULL");
        });

        b.Property(x => x.Code).HasMaxLength(20).IsUnicode(false).IsRequired();
        b.Property(x => x.CreatedById).HasMaxLength(450).IsRequired();
        b.Property(x => x.CashierId).HasMaxLength(450);
        b.Property(x => x.SubTotal).HasPrecision(18, 0);
        b.Property(x => x.Discount).HasPrecision(18, 0);
        b.Property(x => x.Total).HasPrecision(18, 0);
        b.Property(x => x.CashReceived).HasPrecision(18, 0);
        b.Property(x => x.DiscountReason).HasMaxLength(255);
        b.Property(x => x.CancelReason).HasMaxLength(255);
        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId);
        b.HasOne(x => x.Table).WithMany().HasForeignKey(x => x.TableId);
        b.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById);
        b.HasOne(x => x.Cashier).WithMany().HasForeignKey(x => x.CashierId);

        b.HasIndex(x => new { x.BranchId, x.Code }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.BranchId, x.Status });   // đơn đang phục vụ
        b.HasIndex(x => new { x.TenantId, x.PaidAt })                  // báo cáo doanh thu
            .IncludeProperties(x => new { x.BranchId, x.Total });
    }
}
