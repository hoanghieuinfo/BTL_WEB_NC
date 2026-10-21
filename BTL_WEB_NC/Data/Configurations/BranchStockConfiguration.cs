// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BTL_WEB_NC.Data.Configurations;

/// <summary>Bảng 3.15 – BranchStocks. Quantity được phép âm nên không có CHECK.</summary>
public class BranchStockConfiguration : IEntityTypeConfiguration<BranchStock>
{
    public void Configure(EntityTypeBuilder<BranchStock> b)
    {
        b.ToTable("BranchStocks");

        b.HasKey(x => new { x.BranchId, x.IngredientId });
        b.Property(x => x.Quantity).HasPrecision(18, 3);
        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId);
        b.HasOne(x => x.Ingredient).WithMany().HasForeignKey(x => x.IngredientId);

        b.HasIndex(x => new { x.TenantId, x.BranchId });
        b.HasIndex(x => x.IngredientId);
    }
}
