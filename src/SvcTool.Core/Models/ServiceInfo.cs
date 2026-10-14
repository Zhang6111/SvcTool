namespace SvcTool.Core.Models;

/// <summary>
/// 一个 Windows 服务的完整信息快照。
/// </summary>
/// <remarks>
/// 这是一个不可变的数据载体：所有属性都是 init-only，构造完成后不能修改。
/// 服务状态发生变化时，用 with 表达式生成新的实例，而不是就地改这个对象。
/// 这样做的好处是数据来源单一，界面不会读到"改了一半"的状态。
///
/// 用 record 而不是 class，就是为了拿到 with 表达式。
///
/// 各类信息的获取途径见 设计文档.md 第 4.1 节。
/// </remarks>
public sealed record ServiceInfo
{
    /// <summary>
    /// 服务的内部名称，例如 Spooler。
    /// 这是系统接口用来定位服务的唯一标识，创建后不可更改。
    /// </summary>
    public required string ServiceName { get; init; }

    /// <summary>
    /// 服务的显示名，例如"Print Spooler"。
    /// 允许包含空格和中文，仅用于人看。
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>服务描述。可能为空字符串，不会为 null。</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>当前运行状态。</summary>
    public ServiceStatus Status { get; init; } = ServiceStatus.Unknown;

    /// <summary>服务分类。由分类算法计算得出，不是系统直接提供的。</summary>
    public ServiceCategory Category { get; init; } = ServiceCategory.User;

    /// <summary>启动方式。</summary>
    public ServiceStartMode StartMode { get; init; } = ServiceStartMode.Unknown;

    /// <summary>
    /// 是否为"延迟自动启动"。
    /// 仅在 <see cref="StartMode"/> 为 <see cref="ServiceStartMode.Automatic"/> 时有意义。
    /// </summary>
    public bool DelayedAutoStart { get; init; }

    /// <summary>
    /// 运行该服务的账户，例如 "LocalSystem"、"NT AUTHORITY\NetworkService"，
    /// 或具体的 "域\用户名"。
    /// </summary>
    public string Account { get; init; } = string.Empty;

    /// <summary>
    /// 可执行文件的完整路径，已剥离引号和启动参数。
    /// 原始字符串里这两者是混在一起的，剥离逻辑见 设计文档.md 第 5.2 节。
    /// </summary>
    public string BinaryPath { get; init; } = string.Empty;

    /// <summary>启动参数。没有参数时为空字符串。</summary>
    public string Arguments { get; init; } = string.Empty;

    /// <summary>
    /// 可执行文件当前是否存在。
    /// 文件被移动或删除时为 false，界面应当把该行标红提示。
    /// </summary>
    public bool BinaryExists { get; init; } = true;

    /// <summary>服务对应进程的 ID。服务未运行时为 0。</summary>
    public uint ProcessId { get; init; }

    /// <summary>进程占用的内存，单位字节。服务未运行时为 0。</summary>
    public long MemoryBytes { get; init; }

    /// <summary>
    /// 是否由本工具创建。
    /// 记录在 %ProgramData%\SvcTool\created-services.json，见 设计文档.md 第 5.4 节。
    /// </summary>
    public bool IsCreatedByThisTool { get; init; }

    /// <summary>
    /// 状态的界面文案，例如"运行中"。
    /// </summary>
    /// <remarks>
    /// 放在模型上而不是在界面层写值转换器，是刻意的取舍：本项目只面向中文用户，
    /// 这些映射放在这里能用单元测试覆盖，界面代码也能保持很薄。
    /// 这类属性没有后备字段，不参与 record 的相等性比较。
    /// </remarks>
    public string StatusText => Status.ToDisplayName();

    /// <summary>启动方式的界面文案，例如"自动（延迟）"。</summary>
    public string StartModeText => StartMode.ToDisplayName(DelayedAutoStart);

    /// <summary>分类的界面文案，例如"自建"。</summary>
    public string CategoryText => Category.ToDisplayName();

    /// <summary>登录身份的界面文案，例如"本地系统"。</summary>
    public string AccountText => ServiceDisplayNames.ToAccountDisplayName(Account);

    /// <summary>内存占用的界面文案，例如"47.2 MB"。服务未运行时为空字符串。</summary>
    public string MemoryText => ServiceDisplayNames.ToMemoryDisplayName(MemoryBytes);

    /// <summary>
    /// 是否允许执行"停止"操作。
    /// </summary>
    /// <remarks>
    /// 驱动服务恒为 false —— 这不是我们偷懒，而是 Windows 的服务控制接口
    /// 本身就不支持停止驱动。界面上要把停止按钮禁用并说明原因。
    /// </remarks>
    public bool CanStop => Category != ServiceCategory.Driver;

    /// <summary>
    /// 是否允许执行"删除"操作。
    /// </summary>
    /// <remarks>
    /// 系统自带的服务恒为 false。删除 RpcSs 这类关键服务会导致系统无法启动，
    /// 这条限制是不可关闭的，见 需求文档.md 的 NFR-04。
    /// </remarks>
    public bool CanDelete => Category != ServiceCategory.System;

    /// <summary>当前是否处于"过渡中"状态（正在启动或正在停止）。</summary>
    public bool IsPending => Status
        is ServiceStatus.StartPending
        or ServiceStatus.StopPending
        or ServiceStatus.PausePending
        or ServiceStatus.ContinuePending;
}
