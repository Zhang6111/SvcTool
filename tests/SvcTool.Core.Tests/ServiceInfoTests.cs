using SvcTool.Core.Models;

namespace SvcTool.Core.Tests;

/// <summary>
/// <see cref="ServiceInfo"/> 上那几个安全限制属性的测试。
/// </summary>
/// <remarks>
/// CanStop / CanDelete 直接决定界面上按钮的禁用状态，而它们守的是
/// 需求文档 NFR-04 里那几条不可关闭的安全规则，所以必须钉住：
///
///   驱动服务不能停止（Windows 接口不支持）
///   系统自带服务不能删除（删了可能开不了机）
/// </remarks>
[TestClass]
public sealed class ServiceInfoTests
{
    [TestMethod]
    public void CanStop_ForDriver_IsFalse()
        => Assert.IsFalse(CreateService(ServiceCategory.Driver).CanStop);

    [TestMethod]
    public void CanStop_ForSystemAndUser_IsTrue()
    {
        Assert.IsTrue(CreateService(ServiceCategory.System).CanStop);
        Assert.IsTrue(CreateService(ServiceCategory.User).CanStop);
    }

    [TestMethod]
    public void CanDelete_ForSystemService_IsFalse()
        => Assert.IsFalse(CreateService(ServiceCategory.System).CanDelete);

    [TestMethod]
    public void CanDelete_ForDriverAndUser_IsTrue()
    {
        // 驱动服务允许删除（删掉后需要重启计算机才彻底消失，界面另行提示）
        Assert.IsTrue(CreateService(ServiceCategory.Driver).CanDelete);
        Assert.IsTrue(CreateService(ServiceCategory.User).CanDelete);
    }

    [TestMethod]
    public void IsPending_IsTrueOnlyForTransitionStates()
    {
        Assert.IsTrue(CreateService(ServiceCategory.User, ServiceStatus.StartPending).IsPending);
        Assert.IsTrue(CreateService(ServiceCategory.User, ServiceStatus.StopPending).IsPending);
        Assert.IsFalse(CreateService(ServiceCategory.User, ServiceStatus.Running).IsPending);
        Assert.IsFalse(CreateService(ServiceCategory.User, ServiceStatus.Stopped).IsPending);
    }

    [TestMethod]
    public void DisplayTextProperties_AreDerivedFromRawValues()
    {
        var service = CreateService(ServiceCategory.User, ServiceStatus.Running) with
        {
            StartMode = ServiceStartMode.Automatic,
            DelayedAutoStart = true,
            Account = "LocalSystem",
            MemoryBytes = 1024 * 1024,
        };

        Assert.AreEqual("运行中", service.StatusText);
        Assert.AreEqual("自动（延迟）", service.StartModeText);
        Assert.AreEqual("自建", service.CategoryText);
        Assert.AreEqual("本地系统", service.AccountText);
        Assert.AreEqual("1.0 MB", service.MemoryText);
    }

    private static ServiceInfo CreateService(
        ServiceCategory category,
        ServiceStatus status = ServiceStatus.Running) => new()
        {
            ServiceName = "TestSvc",
            DisplayName = "测试服务",
            Category = category,
            Status = status,
        };
}
