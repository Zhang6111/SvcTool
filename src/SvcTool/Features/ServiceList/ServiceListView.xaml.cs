using System.Windows.Controls;

namespace SvcTool.Features.ServiceList;

/// <summary>
/// 服务列表页的界面部分。
/// </summary>
/// <remarks>
/// 按 设计文档.md 第 1.7 节的纪律一：代码隐藏文件里只允许有 InitializeComponent，
/// 任何逻辑都不许写在这里。要加行为请加到 <see cref="ServiceListViewModel"/>。
/// </remarks>
public partial class ServiceListView : UserControl
{
    /// <summary>初始化视图。</summary>
    public ServiceListView()
    {
        InitializeComponent();
    }
}
