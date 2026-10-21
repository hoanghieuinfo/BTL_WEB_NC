// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BTL_WEB_NC.Data.Configurations;

/// <summary>Bảng 3.9.</summary>
public class ServicePlanConfiguration : IEntityTypeConfiguration<ServicePlan>
{
    public void Configure(EntityTypeBuilder<ServicePlan> b)
    {
        b.ToTable("ServicePlans", t =>
        {
            t.HasCheckConstraint("CK_ServicePlans_MaxBranches", "[MaxBranches] > 0");
            t.HasCheckConstraint("CK_ServicePlans_MaxStaff", "[MaxStaff] > 0");
            t.HasCheckConstraint("CK_ServicePlans_PricePerMonth", "[PricePerMonth] >= 0");
        });

        b.Property(x => x.Name).HasMaxLength(50).IsRequired();
        b.HasIndex(x => x.Name).IsUnique();
        b.Property(x => x.PricePerMonth).HasPrecision(18, 0);
    }
}
