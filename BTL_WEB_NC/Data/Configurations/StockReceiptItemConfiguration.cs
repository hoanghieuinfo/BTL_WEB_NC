// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BTL_WEB_NC.Data.Configurations;

/// <summary>Bảng 3.16 – StockReceiptItems. FK tới StockReceipts (CASCADE) đặt trong AppDbContext.</summary>
public class StockReceiptItemConfiguration : IEntityTypeConfiguration<StockReceiptItem>
{
    public void Configure(EntityTypeBuilder<StockReceiptItem> b)
    {
        b.ToTable("StockReceiptItems", t =>
        {
            t.HasCheckConstraint("CK_StockReceiptItems_ConversionRate", "[ConversionRate] > 0");
            t.HasCheckConstraint("CK_StockReceiptItems_Quantity", "[Quantity] > 0");
            t.HasCheckConstraint("CK_StockReceiptItems_UnitPrice", "[UnitPrice] >= 0");
        });

        b.Property(x => x.PurchaseUnit).HasMaxLength(20).IsRequired();
        b.Property(x => x.ConversionRate).HasPrecision(18, 3);
        b.Property(x => x.Quantity).HasPrecision(18, 3);
        b.Property(x => x.UnitPrice).HasPrecision(18, 0);

        b.HasOne(x => x.Ingredient).WithMany().HasForeignKey(x => x.IngredientId);

        b.HasIndex(x => new { x.TenantId, x.StockReceiptId });
        b.HasIndex(x => x.IngredientId);
    }
}
