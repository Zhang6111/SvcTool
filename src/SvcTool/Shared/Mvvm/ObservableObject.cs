using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SvcTool.Shared.Mvvm;

/// <summary>
/// 视图模型的基类，负责在属性值变化时通知界面。
/// </summary>
/// <remarks>
/// WPF 的数据绑定依赖 <see cref="INotifyPropertyChanged"/>：属性变了就发一个通知，
/// 界面自动更新，不需要手动去改控件的文字。
///
/// 这是"自己写 MVVM 基础设施"的核心部分。因为项目约束是不引入第三方库，
/// 所以没有使用 CommunityToolkit.Mvvm 那类现成的包。这个类只有几十行，
/// 全项目复用。
///
/// 用法：属性写成下面这样，赋值时调用 <see cref="SetProperty{T}"/> 即可。
/// <code>
/// private string _searchText = string.Empty;
///
/// public string SearchText
/// {
///     get => _searchText;
///     set => SetProperty(ref _searchText, value);
/// }
/// </code>
/// </remarks>
public abstract class ObservableObject : INotifyPropertyChanged
{
    /// <summary>
    /// 属性值发生变化时触发。
    /// </summary>
    /// <remarks>由数据绑定引擎订阅，业务代码不要直接调用。</remarks>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 给字段赋值，并在值确实发生变化时发出属性变化通知。
    /// </summary>
    /// <typeparam name="T">属性类型。</typeparam>
    /// <param name="field">属性的后备字段，必须用 ref 传入。</param>
    /// <param name="value">要写入的新值。</param>
    /// <param name="propertyName">
    /// 属性名，由编译器自动填入，调用方不需要传。
    /// </param>
    /// <returns>值发生了变化并已发出通知时返回 true；值相同未做任何事时返回 false。</returns>
    /// <remarks>
    /// 值相同就不发通知，这一点很重要：界面上的列表如果频繁收到无意义的通知，
    /// 会反复重建视觉元素，滚动位置甚至会被重置。
    /// </remarks>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>
    /// 手动发出属性变化通知。用于"某个属性的值受另一个属性影响"的场景，
    /// 例如列表的过滤结果变化时，需要顺带通知"结果数量"这个属性。
    /// </summary>
    /// <param name="propertyName">属性名，由编译器自动填入。</param>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
