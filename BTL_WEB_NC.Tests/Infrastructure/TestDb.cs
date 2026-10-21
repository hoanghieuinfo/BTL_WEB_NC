// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Data;
using BTL_WEB_NC.Infrastructure.Tenancy;
using BTL_WEB_NC.Models.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB_NC.Tests.Infrastructure;

/// <summary>
/// CSDL SQLite in-memory dùng chung trong một test. Kết nối mở suốt vòng đời đối tượng,
/// mỗi lần CreateContext tạo context mới với tenant tùy ý (giống request của người dùng khác nhau).
/// </summary>
public sealed class TestDb : IDisposable
{
    public const int TenantA = 1;
    public const int TenantB = 2;

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    /// <param name="seedTenants">false → CSDL trống hoàn toàn (dùng cho test SeedData).</param>
    public TestDb(bool seedTenants = true)
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;

        using var db = CreateContext(tenantId: null);
        db.Database.EnsureCreated();
        if (!seedTenants)
        {
            return;
        }

        // Seed không có tenant (giống SeedData/đăng ký UC01): Tenants không phải ITenantEntity.
        var plan = new ServicePlan { Name = "Cơ bản", MaxBranches = 1, MaxStaff = 5, PricePerMonth = 0 };
        db.ServicePlans.Add(plan);
        db.Tenants.AddRange(
            new Tenant { Id = TenantA, Name = "Quán A", Slug = "quan-a", ServicePlan = plan, Status = TenantStatus.Active, CreatedAt = DateTime.UtcNow },
            new Tenant { Id = TenantB, Name = "Quán B", Slug = "quan-b", ServicePlan = plan, Status = TenantStatus.Active, CreatedAt = DateTime.UtcNow });
        db.SaveChanges();
    }

    public AppDbContext CreateContext(int? tenantId) =>
        new TestAppDbContext(_options, new TenantContext { TenantId = tenantId });

    public void Dispose() => _connection.Dispose();
}

/// <summary>
/// Chỉ dùng trong test, bù hai chỗ SQLite khác SQL Server; mọi cấu hình khác giữ nguyên của AppDbContext:
/// - SQLite không tự sinh rowversion → gán giá trị mặc định để INSERT Orders/BranchStocks không lỗi NOT NULL.
/// - SQLite lưu decimal dạng TEXT nên CHECK "[Discount] = 0" và phép so sánh/cộng tiền sai → lưu decimal dạng REAL.
/// </summary>
internal sealed class TestAppDbContext : AppDbContext
{
    public TestAppDbContext(DbContextOptions<AppDbContext> options, TenantContext tenant) : base(options, tenant)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Order>().Property(x => x.RowVersion).HasDefaultValueSql("randomblob(8)");
        modelBuilder.Entity<BranchStock>().Property(x => x.RowVersion).HasDefaultValueSql("randomblob(8)");

        foreach (var property in modelBuilder.Model.GetEntityTypes()
                     .SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            property.SetProviderClrType(typeof(double));
        }
    }
}
