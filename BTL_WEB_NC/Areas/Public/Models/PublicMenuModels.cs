// [AI] Claude hỗ trợ – Phụ lục Z.7
namespace BTL_WEB_NC.Areas.Public.Models;

public record PublicMenuProduct(string Name, decimal Price, string? ImagePath, string? Description);
public record PublicMenuCategory(string Name, IReadOnlyList<PublicMenuProduct> Products);
public record PublicMenuPage(
    string TenantName, string TenantSlug, string BranchName, string BranchSlug,
    string Address, string? Phone, TimeOnly OpenTime, TimeOnly CloseTime,
    IReadOnlyList<PublicMenuCategory> Categories);

public enum PublicMenuStatus { Found, NotFound, Suspended }
public record PublicMenuResult(PublicMenuStatus Status, PublicMenuPage? Page = null, string? TenantName = null);
public record SitemapEntry(string TenantSlug, string BranchSlug);
