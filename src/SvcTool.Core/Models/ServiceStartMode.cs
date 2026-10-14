namespace SvcTool.Core.Models;

/// <summary>
/// 服务的启动方式。
/// </summary>
/// <remarks>
/// Boot 和 System 只对驱动服务有意义，普通程序的服务不会用到这两个值。
/// </remarks>
public enum ServiceStartMode
{
    /// <summary>未知。读取失败时使用。</summary>
    Unknown = 0,

    /// <summary>由系统加载程序启动。仅驱动服务使用。</summary>
    Boot,

    /// <summary>由系统内核初始化时启动。仅驱动服务使用。</summary>
    System,

    /// <summary>开机后自动启动。</summary>
    Automatic,

    /// <summary>需要时手动启动，或由其它服务触发。</summary>
    Manual,

    /// <summary>已禁用，无法启动。</summary>
    Disabled,
}
