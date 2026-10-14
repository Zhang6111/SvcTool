using SvcTool.Core.Models;
using SvcTool.Core.Services;

namespace SvcTool.Core.Tests;

/// <summary>
/// 假实现的测试。
/// </summary>
/// <remarks>
/// 虽然是"假"的，但它的行为要和真实系统保持一致，否则界面开发时的验证就没意义了。
/// 尤其是驱动服务不能停止、不存在的服务要报错这两条，必须和真实实现同款。
///
/// 构造时传 TimeSpan.Zero 跳过模拟延迟，让测试跑得快。
/// </remarks>
[TestClass]
public sealed class FakeServiceManagerTests
{
    private static FakeServiceManager CreateManager() => new(TimeSpan.Zero);

    [TestMethod]
    public async Task GetAllAsync_ReturnsSampleData()
    {
        var services = await CreateManager().GetAllAsync();

        Assert.AreNotEqual(0, services.Count);
    }

    [TestMethod]
    public async Task GetAllAsync_SampleDataCoversAllThreeCategories()
    {
        var services = await CreateManager().GetAllAsync();

        // 三种分类都要有，否则界面上没法验证分类筛选
        Assert.IsTrue(services.Any(s => s.Category == ServiceCategory.Driver), "样例数据缺少驱动服务");
        Assert.IsTrue(services.Any(s => s.Category == ServiceCategory.System), "样例数据缺少系统服务");
        Assert.IsTrue(services.Any(s => s.Category == ServiceCategory.User), "样例数据缺少自建服务");
    }

    [TestMethod]
    public async Task GetAllAsync_ReturnsSnapshot_NotInternalList()
    {
        var manager = CreateManager();
        var first = await manager.GetAllAsync();
        var second = await manager.GetAllAsync();

        // 两次调用返回的应当是各自独立的快照，而不是同一个可变集合
        Assert.AreNotSame(first, second);
    }

    [TestMethod]
    public async Task GetAsync_IsCaseInsensitive()
    {
        var service = await CreateManager().GetAsync("mysql80");

        Assert.IsNotNull(service);
        Assert.AreEqual("MySQL80", service.ServiceName);
    }

    [TestMethod]
    public async Task GetAsync_ForUnknownService_ReturnsNull()
        => Assert.IsNull(await CreateManager().GetAsync("NoSuchService"));

    [TestMethod]
    public async Task StartAsync_OnStoppedService_MakesItRunningAndAssignsProcessId()
    {
        var manager = CreateManager();

        await manager.StartAsync("MySQL80");

        var service = await manager.GetAsync("MySQL80");
        Assert.IsNotNull(service);
        Assert.AreEqual(ServiceStatus.Running, service.Status);
        Assert.AreNotEqual(0u, service.ProcessId);
    }

    [TestMethod]
    public async Task StopAsync_OnRunningService_MakesItStoppedAndClearsProcessId()
    {
        var manager = CreateManager();

        await manager.StopAsync("MySyncTool");

        var service = await manager.GetAsync("MySyncTool");
        Assert.IsNotNull(service);
        Assert.AreEqual(ServiceStatus.Stopped, service.Status);
        Assert.AreEqual(0u, service.ProcessId);
    }

    [TestMethod]
    public async Task StopAsync_OnDriverService_ThrowsWithChineseExplanation()
    {
        var manager = CreateManager();

        var exception = await Assert.ThrowsExactlyAsync<ServiceOperationException>(
            () => manager.StopAsync("WfpLwFilter"));

        Assert.AreEqual(1052, exception.ErrorCode);
        StringAssert.Contains(exception.Message, "驱动服务");
    }

    [TestMethod]
    public async Task StartAsync_OnUnknownService_ThrowsServiceNotFound()
    {
        var manager = CreateManager();

        var exception = await Assert.ThrowsExactlyAsync<ServiceOperationException>(
            () => manager.StartAsync("NoSuchService"));

        Assert.AreEqual(1060, exception.ErrorCode);
    }

    [TestMethod]
    public async Task StartAsync_OnAlreadyRunningService_Throws()
    {
        var manager = CreateManager();

        var exception = await Assert.ThrowsExactlyAsync<ServiceOperationException>(
            () => manager.StartAsync("Spooler"));

        Assert.AreEqual(1056, exception.ErrorCode);
    }

    [TestMethod]
    public async Task RestartAsync_LeavesServiceRunning()
    {
        var manager = CreateManager();

        await manager.RestartAsync("MySyncTool");

        var service = await manager.GetAsync("MySyncTool");
        Assert.IsNotNull(service);
        Assert.AreEqual(ServiceStatus.Running, service.Status);
    }

    [TestMethod]
    public async Task Operations_AcceptServiceNameInAnyCase()
    {
        var manager = CreateManager();

        await manager.StopAsync("MYSYNCTOOL");

        var service = await manager.GetAsync("MySyncTool");
        Assert.IsNotNull(service);
        Assert.AreEqual(ServiceStatus.Stopped, service.Status);
    }

    [TestMethod]
    public async Task Operations_WithBlankServiceName_ThrowArgumentException()
    {
        var manager = CreateManager();

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => manager.StartAsync("  "));
    }
}
