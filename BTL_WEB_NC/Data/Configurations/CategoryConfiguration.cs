// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BTL_WEB_NC.Data.Configurations;

/// <summary>Bảng 3.14 – Categories.</summary>
public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.ToTable("Categories");

        b.Property(x => x.Name).HasMaxLength(50).IsRequired();

        // UNIQUE có điều kiện: tên của bản ghi đã xóa mềm được dùng lại (Bảng 3.18).
        b.HasIndex(x => new { x.TenantId, x.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
