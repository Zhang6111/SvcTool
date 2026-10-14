using System.Windows;
using SvcTool.Core.Services;
using SvcTool.Features.ServiceList;
using SvcTool.Themes;
using SvcTool.Views;

namespace SvcTool;

/// <summary>
/// 应用程序入口，同时是"组合根"。
/// </summary>
/// <remarks>
/// 组合根的意思是：整个程序只有这一个地方 new 具体的实现类，
/// 其它地方一律只接收接口。见 设计文档.md 第 1.6 节。
///
/// 这里刻意不使用依赖注入容器。这个体量用容器只会增加一层看不懂的间接，
/// 手工组装反而更清楚：想看某个对象是怎么来的，看这个文件就够了。
/// </remarks>
public partial class App : Application
{
    /// <summary>
    /// 程序启动时创建主窗口，并把视图模型注入进去。
    /// </summary>
    /// <param name="e">启动参数。</param>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ------------------------------------------------------------------
        // 让自定义颜色跟随系统浅色/深色设置
        //
        // 必须在创建窗口之前调用，否则窗口会先按默认主题渲染一次，出现闪色。
        // 原因是 WPF 没有 ThemeMode 那样的自定义颜色切换功能，得自己做。
        // 详见 Themes/ThemeManager.cs 的说明。
        // ------------------------------------------------------------------
        ThemeManager.Initialize();

        // ------------------------------------------------------------------
        // 组合根：所有具体实现都在这里组装
        //
        // M1 阶段把 FakeServiceManager 换成调用真实 Windows 接口的实现即可，
        // 视图模型和界面不需要任何改动 —— 这正是接口隔离的价值。
        // ------------------------------------------------------------------
        IServiceManager serviceManager = new FakeServiceManager();
        var viewModel = new ServiceListViewModel(serviceManager);

        var window = new MainWindow
        {
            DataContext = viewModel,
        };
        window.Show();

        // 先把窗口显示出来，再异步读取数据，避免启动时界面空白干等。
        // LoadAsync 内部已经接住了所有异常并转成界面上的错误提示，
        // 所以这里不需要再 await 或 try。
        _ = viewModel.LoadAsync();
    }
}
