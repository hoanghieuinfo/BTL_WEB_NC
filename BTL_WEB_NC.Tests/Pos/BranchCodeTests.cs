// [AI] Claude hỗ trợ – Phụ lục Z.7
using BTL_WEB_NC.Infrastructure;

namespace BTL_WEB_NC.Tests.Pos;

/// <summary>Mã viết tắt chi nhánh dùng trong số hóa đơn (BranchCode.FromName).</summary>
public class BranchCodeTests
{
    [Theory]
    [InlineData("Cầu Giấy", "CG")]
    [InlineData("Hoàn Kiếm", "HK")]
    [InlineData("Thanh Xuân", "TX")]
    [InlineData("Đống Đa", "DD")]
    [InlineData("  Ba   Đình  ", "BD")]
    [InlineData("cầu giấy", "CG")]
    public void FromName_TakesUppercaseInitialsWithoutDiacritics(string name, string expected)
    {
        Assert.Equal(expected, BranchCode.FromName(name));
    }

    [Fact]
    public void FromName_LimitsToFourLetters()
    {
        Assert.Equal("NVAB", BranchCode.FromName("Nguyễn Văn An Bình Chi"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("--- !!!")]
    public void FromName_NoLetterOrDigit_FallsBackToCN(string name)
    {
        Assert.Equal("CN", BranchCode.FromName(name));
    }
}
