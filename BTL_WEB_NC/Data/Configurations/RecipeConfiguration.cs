// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BTL_WEB_NC.Data.Configurations;

/// <summary>Bảng 3.15 – Recipes.</summary>
public class RecipeConfiguration : IEntityTypeConfiguration<Recipe>
{
    public void Configure(EntityTypeBuilder<Recipe> b)
    {
        b.ToTable("Recipes", t =>
            t.HasCheckConstraint("CK_Recipes_Quantity", "[Quantity] > 0"));

        b.HasKey(x => new { x.ProductId, x.IngredientId });
        b.Property(x => x.Quantity).HasPrecision(18, 3);

        b.HasOne(x => x.Product).WithMany(p => p.Recipes).HasForeignKey(x => x.ProductId);
        b.HasOne(x => x.Ingredient).WithMany().HasForeignKey(x => x.IngredientId);

        // PK bắt đầu bằng ProductId theo spec; thêm chỉ mục bắt đầu bằng TenantId cho truy vấn theo tenant.
        b.HasIndex(x => new { x.TenantId, x.ProductId });
        b.HasIndex(x => x.IngredientId);
    }
}
