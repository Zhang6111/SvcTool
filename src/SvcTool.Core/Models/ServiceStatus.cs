namespace SvcTool.Core.Models;

/// <summary>
/// 服务的当前运行状态。
/// </summary>
/// <remarks>
/// 取值与 Windows 服务控制管理器返回的状态一一对应，只是换成了 C# 的命名，
/// 避免魔法数字散落在代码各处。
///
/// 带 Pending 字样的状态表示"过渡中"：系统接口调用返回时服务并没有真正完成
/// 启动或停止，必须轮询确认最终状态，详见 设计文档.md 第 6.1 节。
/// </remarks>
public enum ServiceStatus
{
    /// <summary>未知状态。读取失败时使用，界面应显示为未知而不是崩溃。</summary>
    Unknown = 0,

    /// <summary>已停止。</summary>
    Stopped,

    /// <summary>正在启动，尚未进入运行状态。</summary>
    StartPending,

    /// <summary>正在停止，尚未完全停止。</summary>
    StopPending,

    /// <summary>正在运行。</summary>
    Running,

    /// <summary>正在从暂停状态恢复。</summary>
    ContinuePending,

    /// <summary>正在暂停。</summary>
    PausePending,

    /// <summary>已暂停。</summary>
    Paused,
}
