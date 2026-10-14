using System.Windows;
using Microsoft.Win32;

namespace SvcTool.Themes;

/// <summary>
/// 让应用自定义的颜色跟随 Windows 的浅色/深色设置。
/// </summary>
/// <remarks>
/// ================== 为什么需要这个类 ==================
///
/// WPF 没有 WinUI/UWP 那种 ThemeDictionaries 功能，自定义颜色不会自动跟随主题。
///
/// 内置的 Fluent 主题（App.xaml 里的 ThemeMode="System"）只负责标准控件的外观，
/// 我们自己定义的那些颜色键它管不着，所以必须自己实现切换。
///
/// 做法：
///   Colors.Light.xaml 和 Colors.Dark.xaml 各存一套完整的主题色，
///   App.xaml 先把其中一份合并进来，本类负责在系统主题变化时把那一份换掉。
///
/// 界面里所有主题色都必须用 DynamicResource 引用。用 StaticResource 的话，
/// 字典被换成另一份之后界面不会重新取值，颜色就"卡"在旧主题上。
///
/// ================== 与 ThemeMode 的关系 ==================
///
/// ThemeMode="System" 负责标准控件的浅色/深色，本类负责自定义颜色。
/// 两者都读同一个系统设置，所以表现是一致的。
/// </remarks>
public static class ThemeManager
{
    /// <summary>浅色主题字典的文件名，用于在合并字典里定位"主题槽"。</summary>
    private const string LightFileName = "Colors.Light.xaml";

    /// <summary>深色主题字典的文件名。</summary>
    private const string DarkFileName = "Colors.Dark.xaml";

    private static readonly Uri LightUri = new($"Themes/{LightFileName}", UriKind.Relative);
    private static readonly Uri DarkUri = new($"Themes/{DarkFileName}", UriKind.Relative);

    /// <summary>
    /// 读取当前的系统主题设置并应用，然后持续监听后续的变化。
    /// </summary>
    /// <remarks>
    /// 必须在创建主窗口之前调用一次。重复调用是安全的。
    /// </remarks>
    public static void Initialize()
    {
        Apply(IsSystemUsingDarkTheme());

        // 订阅之后用户在「设置 → 个性化 → 颜色」里切换主题，界面会立刻跟随。
        // 这个事件在非界面线程上触发，所以 Apply 内部会切回界面线程。
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    /// <summary>
    /// 判断 Windows 当前是否使用深色模式。
    /// </summary>
    /// <returns>使用深色主题时返回 true。</returns>
    /// <remarks>
    /// 读的是「应用模式」而不是「Windows 模式」。用户可以把系统界面设成深色、
    /// 应用设成浅色，我们跟随的是后者，这才是应用应该跟的那个。
    /// </remarks>
    public static bool IsSystemUsingDarkTheme()
    {
        const string personalizeKey =
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

        // AppsUseLightTheme：1 表示应用用浅色，0 表示用深色。
        // 读不到时按浅色处理，这是更安全的默认值。
        var value = Registry.GetValue(personalizeKey, "AppsUseLightTheme", 1);
        return value is int useLight && useLight == 0;
    }

    private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        if (dispatcher.CheckAccess())
        {
            Apply(IsSystemUsingDarkTheme());
        }
        else
        {
            dispatcher.Invoke(() => Apply(IsSystemUsingDarkTheme()));
        }
    }

    /// <summary>
    /// 把应用资源里的"主题槽"换成指定主题的那一份字典。
    /// </summary>
    /// <param name="useDark">true 表示切换到深色主题。</param>
    private static void Apply(bool useDark)
    {
        var dictionaries = Application.Current?.Resources.MergedDictionaries;
        if (dictionaries is null)
        {
            return;
        }

        var slotIndex = FindThemeSlotIndex(dictionaries);
        if (slotIndex < 0)
        {
            // App.xaml 里没找到主题槽，说明装配出了问题。这里不抛异常，
            // 因为抛了也只会让程序起不来，界面顶多是颜色不对。
            return;
        }

        var targetFileName = useDark ? DarkFileName : LightFileName;
        var currentSource = dictionaries[slotIndex].Source?.OriginalString ?? string.Empty;

        if (currentSource.EndsWith(targetFileName, StringComparison.OrdinalIgnoreCase))
        {
            // 已经是目标主题，不用换。避免无意义地重建字典导致界面闪烁。
            return;
        }

        dictionaries[slotIndex] = new ResourceDictionary
        {
            Source = useDark ? DarkUri : LightUri,
        };
    }

    /// <summary>
    /// 在合并字典里找到"主题槽"的位置。
    /// </summary>
    /// <param name="dictionaries">应用的合并字典集合。</param>
    /// <returns>主题槽的索引；找不到时返回 -1。</returns>
    private static int FindThemeSlotIndex(IList<ResourceDictionary> dictionaries)
    {
        for (var i = 0; i < dictionaries.Count; i++)
        {
            var source = dictionaries[i].Source?.OriginalString;
            if (source is null)
            {
                continue;
            }

            if (source.EndsWith(LightFileName, StringComparison.OrdinalIgnoreCase)
                || source.EndsWith(DarkFileName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }
}
