using SvcTool.Core.Models;

namespace SvcTool.Core.Tests;

/// <summary>
/// 枚举值到中文文案的映射测试。
/// </summary>
/// <remarks>
/// 这些映射虽然简单，但一旦写错会让界面显示得莫名其妙，
/// 而且很容易在改动枚举时漏掉分支，所以值得用测试钉住。
/// </remarks>
[TestClass]
public sealed class ServiceDisplayNamesTests
{
    [TestMethod]
    public void ToDisplayName_ForRunning_ReturnsChineseWord()
        => Assert.AreEqual("运行中", ServiceStatus.Running.ToDisplayName());

    [TestMethod]
    public void ToDisplayName_ForUnknownStatus_ReturnsFallbackInsteadOfThrowing()
        => Assert.AreEqual("未知", ServiceStatus.Unknown.ToDisplayName());

    [TestMethod]
    public void ToDisplayName_ForAutomaticWithDelay_IncludesDelayHint()
        => Assert.AreEqual("自动（延迟）", ServiceStartMode.Automatic.ToDisplayName(delayed: true));

    [TestMethod]
    public void ToDisplayName_ForAutomaticWithoutDelay_IsPlainAutomatic()
        => Assert.AreEqual("自动", ServiceStartMode.Automatic.ToDisplayName(delayed: false));

    [TestMethod]
    public void ToAccountDisplayName_ForLocalSystem_IsTranslated()
        => Assert.AreEqual("本地系统", ServiceDisplayNames.ToAccountDisplayName("LocalSystem"));

    [TestMethod]
    public void ToAccountDisplayName_ForNetworkService_IsTranslated()
        => Assert.AreEqual("网络服务", ServiceDisplayNames.ToAccountDisplayName(@"NT AUTHORITY\NetworkService"));

    [TestMethod]
    public void ToAccountDisplayName_ForDomainAccount_IsKeptAsIs()
        => Assert.AreEqual(@"CONTOSO\svcuser", ServiceDisplayNames.ToAccountDisplayName(@"CONTOSO\svcuser"));

    [TestMethod]
    public void ToAccountDisplayName_ForEmpty_ReturnsUnknownInsteadOfEmpty()
        => Assert.AreEqual("未知", ServiceDisplayNames.ToAccountDisplayName("   "));

    [TestMethod]
    public void ToMemoryDisplayName_FormatsMegabytes()
        => Assert.AreEqual("47.0 MB", ServiceDisplayNames.ToMemoryDisplayName(47L * 1024 * 1024));

    [TestMethod]
    public void ToMemoryDisplayName_ForZero_ReturnsEmptyString()
        => Assert.AreEqual(string.Empty, ServiceDisplayNames.ToMemoryDisplayName(0));
}
