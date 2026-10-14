using SvcTool.Core.Models;

namespace SvcTool.Core.Services;

/// <summary>
/// <see cref="IServiceManager"/> 的假实现：用一批写死的数据模拟真实服务。
/// </summary>
/// <remarks>
/// 存在的意义：
///
///   1. 开发界面时不需要管理员权限、更不会碰任何真实服务
///   2. 让界面层可以脱离系统调用独立开发（M0、M1 阶段的界面就靠它驱动）
///   3. 为真实实现（M1 的 ServiceManager）提供对照标准
///
/// 数据刻意覆盖了三种分类和多种状态，包括"正在启动"和"程序文件不存在"
/// 这两种在界面上需要特殊表现的情况。
/// </remarks>
public sealed class FakeServiceManager : IServiceManager
{
    private readonly List<ServiceInfo> _services;
    private readonly Lock _gate = new();
    private readonly TimeSpan _operationDelay;
    private uint _nextProcessId = 8000;

    /// <summary>初始化假实现，并载入内置的模拟数据。</summary>
    /// <param name="operationDelay">
    /// 模拟启动/停止操作的耗时。默认 400 毫秒，用来让界面能看到"正在启动"的过渡状态。
    /// 单元测试里传 <see cref="TimeSpan.Zero"/> 可以跳过等待。
    /// </param>
    public FakeServiceManager(TimeSpan? operationDelay = null)
    {
        _operationDelay = operationDelay ?? TimeSpan.FromMilliseconds(400);
        _services = CreateSampleServices();
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ServiceInfo>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            // 返回快照而不是内部列表本身，避免调用方在遍历时被并发修改影响
            IReadOnlyList<ServiceInfo> snapshot = [.. _services];
            return Task.FromResult(snapshot);
        }
    }

    /// <inheritdoc />
    public Task<ServiceInfo?> GetAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var found = _services.Find(s => IsSameService(s, serviceName));
            return Task.FromResult(found);
        }
    }

    /// <inheritdoc />
    public async Task StartAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        await Task.Delay(_operationDelay, cancellationToken);

        Mutate(serviceName, service =>
        {
            if (service.Status == ServiceStatus.Running)
            {
                throw new ServiceOperationException(1056, $"服务 {service.DisplayName} 已经在运行。");
            }

            return service with
            {
                Status = ServiceStatus.Running,
                ProcessId = _nextProcessId++,
                MemoryBytes = 42L * 1024 * 1024,
            };
        });
    }

    /// <inheritdoc />
    public async Task StopAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        await Task.Delay(_operationDelay, cancellationToken);

        Mutate(serviceName, service =>
        {
            // 驱动服务在真实系统里无法用停止命令停止，这里如实模拟
            if (service.Category == ServiceCategory.Driver)
            {
                throw new ServiceOperationException(
                    1052,
                    $"驱动服务 {service.DisplayName} 无法通过停止命令停止，Windows 不支持此操作。只能禁用或删除它。");
            }

            if (service.Status == ServiceStatus.Stopped)
            {
                throw new ServiceOperationException(1062, $"服务 {service.DisplayName} 当前未在运行。");
            }

            return service with
            {
                Status = ServiceStatus.Stopped,
                ProcessId = 0,
                MemoryBytes = 0,
            };
        });
    }

    /// <inheritdoc />
    public async Task RestartAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        // 先停后启。真实实现也必须这样，不能两个调用一起发出去。
        await StopAsync(serviceName, cancellationToken);
        await StartAsync(serviceName, cancellationToken);
    }

    private static bool IsSameService(ServiceInfo service, string serviceName) =>
        string.Equals(service.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase);

    private void Mutate(string serviceName, Func<ServiceInfo, ServiceInfo> change)
    {
        lock (_gate)
        {
            var index = _services.FindIndex(s => IsSameService(s, serviceName));
            if (index < 0)
            {
                throw new ServiceOperationException(1060, $"指定的服务不存在：{serviceName}");
            }

            _services[index] = change(_services[index]);
        }
    }

    /// <summary>
    /// 构造模拟数据。
    /// </summary>
    /// <remarks>
    /// 数据都取自真实机器上的典型服务，便于对照效果。
    /// 其中 ReportSvc 的 BinaryExists 刻意设为 false，
    /// 用来验证界面对"程序文件不存在"的表现。
    /// </remarks>
    private static List<ServiceInfo> CreateSampleServices() =>
    [
        new ServiceInfo
        {
            ServiceName = "Spooler",
            DisplayName = "Print Spooler",
            Description = "负责管理打印作业的后台服务。",
            Status = ServiceStatus.Running,
            Category = ServiceCategory.System,
            StartMode = ServiceStartMode.Automatic,
            Account = "LocalSystem",
            BinaryPath = @"C:\Windows\System32\spoolsv.exe",
            ProcessId = 1824,
            MemoryBytes = 28L * 1024 * 1024,
        },
        new ServiceInfo
        {
            ServiceName = "wuauserv",
            DisplayName = "Windows Update",
            Description = "启用 Windows 更新和自动更新的检测、下载与安装。",
            Status = ServiceStatus.Running,
            Category = ServiceCategory.System,
            StartMode = ServiceStartMode.Automatic,
            DelayedAutoStart = true,
            Account = "LocalSystem",
            BinaryPath = @"C:\Windows\system32\svchost.exe",
            Arguments = "-k netsvcs -p",
            ProcessId = 2136,
            MemoryBytes = 63L * 1024 * 1024,
        },
        new ServiceInfo
        {
            ServiceName = "MySQL80",
            DisplayName = "MySQL80",
            Description = "MySQL 数据库服务。",
            Status = ServiceStatus.Stopped,
            Category = ServiceCategory.User,
            StartMode = ServiceStartMode.Manual,
            Account = @"NT AUTHORITY\NetworkService",
            BinaryPath = @"C:\Program Files\MySQL\MySQL Server 8.0\bin\mysqld.exe",
            Arguments = "--defaults-file=\"C:\\ProgramData\\MySQL\\my.ini\" MySQL80",
        },
        new ServiceInfo
        {
            ServiceName = "MySyncTool",
            DisplayName = "我的同步工具",
            Description = "把内网文件同步到共享目录的后台服务。",
            Status = ServiceStatus.Running,
            Category = ServiceCategory.User,
            StartMode = ServiceStartMode.Automatic,
            Account = "LocalSystem",
            BinaryPath = @"D:\apps\my sync\mysync.exe",
            Arguments = "-c conf.ini",
            ProcessId = 8421,
            MemoryBytes = 47L * 1024 * 1024,
            IsCreatedByThisTool = true,
        },
        new ServiceInfo
        {
            ServiceName = "FooSvc",
            DisplayName = "FooService",
            Description = "测试用的示例服务，用于演示启动中的过渡状态。",
            Status = ServiceStatus.StartPending,
            Category = ServiceCategory.User,
            StartMode = ServiceStartMode.Automatic,
            Account = "LocalSystem",
            BinaryPath = @"D:\apps\foo\foo.exe",
            ProcessId = 9120,
            MemoryBytes = 12L * 1024 * 1024,
        },
        new ServiceInfo
        {
            ServiceName = "WfpLwFilter",
            DisplayName = "WFP LightWeight Filter",
            Description = "Windows 筛选平台轻量筛选器驱动。",
            Status = ServiceStatus.Running,
            Category = ServiceCategory.Driver,
            StartMode = ServiceStartMode.System,
            Account = "LocalSystem",
            BinaryPath = @"C:\Windows\system32\drivers\wfplwfs.sys",
        },
        new ServiceInfo
        {
            ServiceName = "SteamService",
            DisplayName = "Steam Client Service",
            Description = "为 Steam 客户端提供系统级支持。",
            Status = ServiceStatus.Stopped,
            Category = ServiceCategory.User,
            StartMode = ServiceStartMode.Manual,
            Account = "LocalSystem",
            BinaryPath = @"C:\Program Files (x86)\Common Files\Steam\SteamService.exe",
            Arguments = "/RunAsService",
        },
        new ServiceInfo
        {
            ServiceName = "ReportSvc",
            DisplayName = "内部报表服务",
            Description = "生成每日报表。程序文件已被移动，这是刻意构造的异常样例。",
            Status = ServiceStatus.Stopped,
            Category = ServiceCategory.User,
            StartMode = ServiceStartMode.Automatic,
            Account = "LocalSystem",
            BinaryPath = @"D:\tools\report\report.exe",
            BinaryExists = false,
        },
    ];
}
