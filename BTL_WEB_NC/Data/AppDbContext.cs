// [AI] Claude hỗ trợ – Phụ lục Z.7
using System.Reflection;
using BTL_WEB_NC.Infrastructure.Tenancy;
using BTL_WEB_NC.Models.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB_NC.Data;

/// <summary>
/// DbContext dùng chung mọi tenant (shared database – shared schema, mục 3.2.2).
/// - Đọc: Global Query Filter tự thêm điều kiện TenantId (và !IsDeleted) cho mọi ITenantEntity.
/// - Ghi: SaveChanges tự gán TenantId cho bản ghi mới, chặn ghi vào/đổi sang tenant khác.
/// </summary>
public class AppDbContext : IdentityDbContext<AppUser>
{
    private readonly TenantContext _tenant;

    public AppDbContext(DbContextOptions<AppDbContext> options, TenantContext tenant) : base(options)
    {
        _tenant = tenant;
    }

    /// <summary>
    /// Tenant hiện tại. Bộ lọc phải tham chiếu thuộc tính của DbContext (không phải biến cục bộ):
    /// EF Core cache model một lần, nhưng mỗi truy vấn đọc lại thành viên của instance DbContext đang chạy.
    /// </summary>
    public int? CurrentTenantId => _tenant.TenantId;

    public DbSet<ServicePlan> ServicePlans => Set<ServicePlan>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Zone> Zones => Set<Zone>();
    public DbSet<DiningTable> DiningTables => Set<DiningTable>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<BranchStock> BranchStocks => Set<BranchStock>();
    public DbSet<StockReceipt> StockReceipts => Set<StockReceipt>();
    public DbSet<StockReceiptItem> StockReceiptItems => Set<StockReceiptItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        var applyTenantRules = typeof(AppDbContext)
            .GetMethod(nameof(ConfigureTenantEntity), BindingFlags.NonPublic | BindingFlags.Instance)!;

        foreach (var clrType in modelBuilder.Model.GetEntityTypes().Select(t => t.ClrType).ToList())
        {
            if (typeof(ITenantEntity).IsAssignableFrom(clrType))
            {
                applyTenantRules.MakeGenericMethod(clrType).Invoke(this, new object[] { modelBuilder });
            }
        }

        // R42: không xóa dây chuyền dữ liệu đã phát sinh giao dịch → mọi FK nghiệp vụ là NO ACTION.
        // Bảng Identity (AspNetUserRoles, AspNetUserClaims…) giữ mặc định của Identity.
        foreach (var fk in modelBuilder.Model.GetEntityTypes()
                     .Where(t => t.ClrType.Namespace == typeof(Tenant).Namespace)
                     .SelectMany(t => t.GetForeignKeys()))
        {
            fk.DeleteBehavior = DeleteBehavior.Restrict;
        }

        // Ngoại lệ duy nhất theo Bảng 3.18: xóa phiếu nhập thì xóa luôn chi tiết.
        modelBuilder.Entity<StockReceiptItem>()
            .HasOne(i => i.StockReceipt)
            .WithMany(r => r.Items)
            .HasForeignKey(i => i.StockReceiptId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    /// <summary>FK TenantId → Tenants và Global Query Filter cho một entity thuộc tenant.</summary>
    private void ConfigureTenantEntity<T>(ModelBuilder modelBuilder) where T : class, ITenantEntity
    {
        modelBuilder.Entity<T>()
            .HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(e => e.TenantId);

        // EF Core 8 chỉ cho một bộ lọc mỗi entity → gộp TenantId và IsDeleted trong cùng một biểu thức.
        // CurrentTenantId null (chưa đăng nhập/Super Admin) → so sánh luôn sai → không trả bản ghi nào.
        if (typeof(ISoftDelete).IsAssignableFrom(typeof(T)))
        {
            modelBuilder.Entity<T>().HasQueryFilter(
                e => e.TenantId == CurrentTenantId && !((ISoftDelete)e).IsDeleted);
        }
        else
        {
            modelBuilder.Entity<T>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyTenantRules();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyTenantRules();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>Kiểm tra TenantId của mọi bản ghi sắp lưu (mục 3.2.2c). Vi phạm → không lưu gì cả.</summary>
    private void ApplyTenantRules()
    {
        var current = CurrentTenantId;

        foreach (var entry in ChangeTracker.Entries<ITenantEntity>())
        {
            var entity = entry.Entity;
            var entityName = entry.Metadata.ClrType.Name;

            switch (entry.State)
            {
                case EntityState.Added:
                    if (entity.TenantId == 0)
                    {
                        // Bản ghi mới chưa có TenantId: lấy tenant hiện tại, không có thì từ chối.
                        entity.TenantId = current
                            ?? throw new TenantViolationException(
                                $"Không xác định được tenant khi thêm {entityName}.");
                    }
                    else if (current.HasValue && entity.TenantId != current.Value)
                    {
                        throw new TenantViolationException(
                            $"Không được thêm {entityName} cho tenant khác.");
                    }
                    // TenantId gán sẵn khi không có tenant: chỉ seed/đăng ký UC01 làm vậy (mục 3.2.2c).
                    break;

                case EntityState.Modified:
                    var tenantProp = entry.Property(e => e.TenantId);
                    if (tenantProp.IsModified && tenantProp.OriginalValue != tenantProp.CurrentValue)
                    {
                        throw new TenantViolationException(
                            $"Không được đổi TenantId của {entityName}.");
                    }
                    // Chặn cả trường hợp Update() một đối tượng tự dựng mang TenantId của tenant khác.
                    if (current.HasValue && entity.TenantId != current.Value)
                    {
                        throw new TenantViolationException(
                            $"Không được sửa {entityName} của tenant khác.");
                    }
                    break;

                case EntityState.Deleted:
                    if (current.HasValue && entity.TenantId != current.Value)
                    {
                        throw new TenantViolationException(
                            $"Không được xóa {entityName} của tenant khác.");
                    }
                    break;
            }
        }
    }
}
