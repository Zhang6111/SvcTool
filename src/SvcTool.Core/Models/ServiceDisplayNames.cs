namespace SvcTool.Core.Models;

/// <summary>
/// 枚举值到界面中文文案的映射。
/// </summary>
/// <remarks>
/// 放在核心层而不是界面层，是一个刻意的取舍：
///
///   1. 本项目只面向中文用户，不存在多语言的诉求
///   2. 放在这里可以用单元测试覆盖（界面层的值转换器很难测）
///   3. 避免在 XAML 里写一堆值转换器，界面代码能保持很薄
///
/// 如果将来要做多语言，再把这些方法挪到界面层并接入资源文件。
/// </remarks>
public static class ServiceDisplayNames
{
    /// <summary>把服务状态转成界面文案。</summary>
    /// <param name="status">服务状态。</param>
    /// <returns>中文状态词，不含颜色和图标。</returns>
    public static string ToDisplayName(this ServiceStatus status) => status switch
    {
        ServiceStatus.Running => "运行中",
        ServiceStatus.Stopped => "已停止",
        ServiceStatus.StartPending => "正在启动",
        ServiceStatus.StopPending => "正在停止",
        ServiceStatus.Paused => "已暂停",
        ServiceStatus.PausePending => "正在暂停",
        ServiceStatus.ContinuePending => "正在恢复",
        _ => "未知",
    };

    /// <summary>把启动方式转成界面文案。</summary>
    /// <param name="mode">启动方式。</param>
    /// <param name="delayed">是否为延迟启动。只有自动启动时才需要区分。</param>
    /// <returns>中文启动方式描述。</returns>
    public static string ToDisplayName(this ServiceStartMode mode, bool delayed = false) => mode switch
    {
        ServiceStartMode.Automatic => delayed ? "自动（延迟）" : "自动",
        ServiceStartMode.Manual => "手动",
        ServiceStartMode.Disabled => "已禁用",
        ServiceStartMode.Boot => "引导启动",
        ServiceStartMode.System => "系统启动",
        _ => "未知",
    };

    /// <summary>把服务分类转成界面文案。</summary>
    /// <param name="category">服务分类。</param>
    /// <returns>徽章上显示的两个字。</returns>
    public static string ToDisplayName(this ServiceCategory category) => category switch
    {
        ServiceCategory.Driver => "驱动",
        ServiceCategory.System => "系统",
        ServiceCategory.User => "自建",
        _ => "未知",
    };

    /// <summary>
    /// 把登录账户转成界面文案。
    /// </summary>
    /// <remarks>
    /// 系统接口返回的是 "LocalSystem"、"NT AUTHORITY\NetworkService" 这类原始值，
    /// 直接显示给用户不友好，这里换成中文说法。无法识别的账户原样返回。
    /// </remarks>
    /// <param name="account">系统接口返回的账户字符串。</param>
    /// <returns>中文账户描述，或原字符串。</returns>
    public static string ToAccountDisplayName(string account)
    {
        if (string.IsNullOrWhiteSpace(account))
        {
            return "未知";
        }

        return account.Trim() switch
        {
            "LocalSystem" => "本地系统",
            "NT AUTHORITY\\LocalService" => "本地服务",
            "NT AUTHORITY\\NetworkService" => "网络服务",
            _ => account,
        };
    }

    /// <summary>把内存占用格式化成便于阅读的形式。</summary>
    /// <param name="bytes">字节数。为 0 时返回空字符串。</param>
    /// <returns>例如 "47.2 MB"，或空字符串。</returns>
    public static string ToMemoryDisplayName(long bytes)
    {
        if (bytes <= 0)
        {
            return string.Empty;
        }

        const double Kb = 1024;
        const double Mb = Kb * 1024;
        const double Gb = Mb * 1024;

        return bytes switch
        {
            >= (long)Gb => $"{bytes / Gb:0.0} GB",
            >= (long)Mb => $"{bytes / Mb:0.0} MB",
            >= (long)Kb => $"{bytes / Kb:0.0} KB",
            _ => $"{bytes} B",
        };
    }
}
