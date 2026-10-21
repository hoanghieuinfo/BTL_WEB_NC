// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BTL_WEB_NC.Data.Configurations;

/// <summary>Bảng 3.11 – cột bổ sung của AspNetUsers.</summary>
public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> b)
    {
        b.Property(x => x.FullName).HasMaxLength(100).IsRequired();

        b.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId);
        b.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId);

        // Identity tạo EmailIndex không duy nhất; spec yêu cầu email duy nhất toàn hệ thống.
        b.HasIndex(x => x.NormalizedEmail)
            .HasDatabaseName("EmailIndex")
            .IsUnique()
            .HasFilter("[NormalizedEmail] IS NOT NULL");

        // AppUser không có bộ lọc tự động; service nhân viên luôn lọc theo TenantId (và BranchId).
        b.HasIndex(x => new { x.TenantId, x.BranchId });
    }
}
