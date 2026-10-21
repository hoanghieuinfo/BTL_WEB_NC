// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BTL_WEB_NC.Data.Configurations;

/// <summary>Bảng 3.14 – Products.</summary>
public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.ToTable("Products", t =>
            t.HasCheckConstraint("CK_Products_Price", "[Price] > 0"));

        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Price).HasPrecision(18, 0);
        b.Property(x => x.ImagePath).HasMaxLength(255).IsUnicode(false);
        b.Property(x => x.Description).HasMaxLength(500);

        b.HasOne(x => x.Category).WithMany(c => c.Products).HasForeignKey(x => x.CategoryId);

        b.HasIndex(x => new { x.TenantId, x.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasIndex(x => new { x.TenantId, x.CategoryId });
    }
}
