// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BTL_WEB_NC.Data.Configurations;

/// <summary>Bảng 3.17 – OrderItems.</summary>
public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> b)
    {
        b.ToTable("OrderItems", t =>
        {
            t.HasCheckConstraint("CK_OrderItems_Quantity", "[Quantity] > 0");
            t.HasCheckConstraint("CK_OrderItems_UnitPrice", "[UnitPrice] >= 0");
        });

        b.Property(x => x.ProductName).HasMaxLength(100).IsRequired();
        b.Property(x => x.UnitPrice).HasPrecision(18, 0);
        b.Property(x => x.Note).HasMaxLength(100);
        b.Property(x => x.CancelReason).HasMaxLength(255);

        b.HasOne(x => x.Order).WithMany(o => o.Items).HasForeignKey(x => x.OrderId);
        b.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId);

        b.HasIndex(x => new { x.TenantId, x.OrderId });
        b.HasIndex(x => x.ProductId);
    }
}
