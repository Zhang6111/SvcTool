using System.Threading;
using System.Windows;
using SvcTool.Themes;

namespace SvcTool.Core.Tests;

/// <summary>
/// 主题样式文件的装配测试。
/// </summary>
/// <remarks>
/// XAML 里的 StaticResource 引用是在运行时解析的，写错键名编译期不报错，
/// 但程序一启动就会抛异常。这个测试把 App.xaml 完整加载一遍，等于把所有
/// 样式字典的装配关系验证了一次。
///
/// 标记为不并行：整个进程只能存在一个 Application 实例，
/// 与其它测试并行会互相干扰。
/// </remarks>
[TestClass]
[DoNotParallelize]
public sealed class ThemeXamlTests
{
    /// <summary>
    /// 加载 App.xaml，确认全部样式字典能被解析、关键资源都能找到。
    /// </summary>
    [TestMethod]
    public void AppResources_LoadWithoutErrors_AndKeyResourcesResolve()
    {
        Exception? failure = null;
        var missingResources = new List<string>();

        // WPF 的 Application 只能在 STA 线程上创建，测试运行器默认是 MTA，
        // 所以这里自己起一个 STA 线程来跑。
        var thread = new Thread(() =>
        {
            try
            {
                var app = new App();
                app.InitializeComponent();

                // 逐个查找关键资源。任何一个找不到，都说明样式字典的合并
                // 或者键名出了问题，程序启动后界面就会缺色或缺样式。
                string[] requiredKeys =
                [
                    // 主题色（与主题无关的部分）
                    "App.Accent",
                    // 主题色（浅色主题槽）
                    "App.Text",
                    "App.Surface",
                    "App.TextOnAccent",
                    "App.StatusRunning",
                    "App.ErrorSoft",
                    "App.CategoryDriver",
                    "App.CategoryUserSoft",
                    // 字体与文本样式
                    "App.FontFamily",
                    "App.Text.PageTitle",
                    "App.Text.Mono",
                    // 图标
                    "Icon.Search",
                    "Icon.Delete",
                    "Icon.Star",
                    "App.Icon",
                    "App.Icon.Solid",
                    // 控件样式
                    "App.Button",
                    "App.Button.Primary",
                    "App.Button.Danger",
                    "App.Button.Icon",
                    "App.TextBox",
                    "App.Card",
                    "App.Badge.Driver",
                    "App.Text.Status",
                ];

                foreach (var key in requiredKeys)
                {
                    if (app.TryFindResource(key) is null)
                    {
                        missingResources.Add(key);
                    }
                }

                // ----------------------------------------------------------
                // 构造主窗口。
                //
                // 这一步会把 MainWindow.xaml 和它里面的 ServiceListView.xaml
                // 一并解析。XAML 里的 StaticResource 引用是在加载时解析的，
                // 少一个键就会抛异常，所以构造一次等于把界面的资源引用全验了一遍。
                //
                // 只构造不显示，不创建任何窗口句柄，不会干扰当前桌面。
                // ----------------------------------------------------------
                var viewModel = new SvcTool.Features.ServiceList.ServiceListViewModel(
                    new SvcTool.Core.Services.FakeServiceManager(TimeSpan.Zero));
                var window = new SvcTool.Views.MainWindow
                {
                    DataContext = viewModel,
                };

                if (window.Content is not SvcTool.Features.ServiceList.ServiceListView)
                {
                    missingResources.Add("MainWindow 的内容不是 ServiceListView");
                }
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.IsNull(failure, $"加载 App.xaml 时抛出异常：{failure}");
        Assert.IsEmpty(missingResources, $"以下资源找不到：{string.Join(", ", missingResources)}");
    }

    /// <summary>
    /// 读取系统主题设置不应抛异常。真实取值取决于测试机的设置，不做断言。
    /// </summary>
    [TestMethod]
    public void IsSystemUsingDarkTheme_DoesNotThrow()
        => _ = ThemeManager.IsSystemUsingDarkTheme();
}
