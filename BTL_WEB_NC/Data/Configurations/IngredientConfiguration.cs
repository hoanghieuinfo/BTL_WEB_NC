// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BTL_WEB_NC.Data.Configurations;

/// <summary>Bảng 3.15 – Ingredients.</summary>
public class IngredientConfiguration : IEntityTypeConfiguration<Ingredient>
{
    public void Configure(EntityTypeBuilder<Ingredient> b)
    {
        b.ToTable("Ingredients", t =>
            t.HasCheckConstraint("CK_Ingredients_MinStock", "[MinStock] >= 0"));

        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Unit).HasMaxLength(20).IsRequired();
        b.Property(x => x.MinStock).HasPrecision(18, 3);

        b.HasIndex(x => new { x.TenantId, x.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
