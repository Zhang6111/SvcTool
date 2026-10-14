using System.Windows.Input;

namespace SvcTool.Shared.Mvvm;

/// <summary>
/// 把一个无参数的同步方法包装成界面可以绑定的命令。
/// </summary>
/// <remarks>
/// WPF 里按钮不直接绑方法，而是绑 <see cref="ICommand"/>。这个类就是最薄的包装：
/// 把普通方法和一个"当前能不能执行"的判断包进去。
///
/// 用这个而不是用事件处理函数的原因见 设计文档.md 第 1.7 节的纪律四：
/// 界面代码里不允许直接操作控件，所有交互都通过绑定和命令。
/// </remarks>
public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    /// <summary>初始化命令。</summary>
    /// <param name="execute">命令被触发时执行的方法。</param>
    /// <param name="canExecute">
    /// 判断当前是否可执行。传 null 表示始终可执行。
    /// 这个方法会被界面频繁调用，必须足够轻量，不要在里面做耗时操作。
    /// </param>
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(execute);

        _execute = execute;
        _canExecute = canExecute;
    }

    /// <summary>
    /// 命令的可执行状态发生变化时触发。
    /// </summary>
    /// <remarks>由数据绑定引擎订阅，业务代码不要直接调用。</remarks>
    public event EventHandler? CanExecuteChanged;

    /// <inheritdoc />
    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    /// <inheritdoc />
    public void Execute(object? parameter) => _execute();

    /// <summary>
    /// 手动通知界面重新评估这个命令能不能执行。
    /// </summary>
    /// <remarks>
    /// 当"能不能执行"取决于某个外部状态（比如列表里有没有选中项）时，
    /// 那个状态一变就要调用这个方法，否则按钮的可用状态不会更新。
    /// </remarks>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
