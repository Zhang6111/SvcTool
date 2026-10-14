namespace SvcTool.Core.Models;

/// <summary>
/// 服务分类。用于界面上分组展示，以及决定哪些操作需要被禁止。
/// </summary>
/// <remarks>
/// 分类的判定逻辑见 设计文档.md 第 5 节。判定依据是服务类型位标志、
/// 可执行文件所在目录、以及数字签名的发布者。
/// </remarks>
public enum ServiceCategory
{
    /// <summary>驱动服务。无法用停止命令停止，只能禁用或删除。</summary>
    Driver,

    /// <summary>
    /// Windows 自带的系统服务。
    /// 界面上必须禁止删除这类服务 —— 删掉关键服务会导致系统无法启动。
    /// </summary>
    System,

    /// <summary>第三方程序安装的服务。</summary>
    User,
}
