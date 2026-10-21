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

    public TestDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;

        using var db = CreateContext(tenantId: null);
        db.Database.EnsureCreated();

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
/// Chỉ dùng trong test: SQLite không tự sinh rowversion như SQL Server, nên gán giá trị mặc định
/// để INSERT Orders/BranchStocks không lỗi NOT NULL. Mọi cấu hình khác giữ nguyên của AppDbContext.
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
    }
}
