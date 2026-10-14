using System.Windows.Input;

namespace SvcTool.Shared.Mvvm;

/// <summary>
/// 把一个返回 <see cref="Task"/> 的异步方法包装成界面可以绑定的命令。
/// </summary>
/// <remarks>
/// 与 <see cref="RelayCommand"/> 的区别有两点，都是为服务操作量身定做的：
///
///   1. <b>执行期间自动禁用自身</b>。服务启停最长要 30 秒，如果不锁住按钮，
///      用户连点几下就会往系统里发出多个冲突的请求。
///   2. <b>异常不会让程序崩掉</b>。<see cref="ICommand.Execute"/> 的签名是 void，
///      里面的异常如果没人接住会直接终止进程。这里统一捕获后交给
///      <c>onFailed</c> 回调，由视图模型决定怎么呈现给用户。
/// </remarks>
public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private readonly Action<Exception>? _onFailed;
    private bool _isRunning;

    /// <summary>初始化异步命令。</summary>
    /// <param name="execute">命令被触发时执行的异步方法。</param>
    /// <param name="canExecute">
    /// 判断当前是否可执行。传 null 表示始终可执行。
    /// 执行期间这个命令一律不可执行，无需在 <paramref name="canExecute"/> 里再判断。
    /// </param>
    /// <param name="onFailed">
    /// 执行过程中抛出异常时的回调。传 null 表示忽略异常（不推荐）。
    /// </param>
    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null, Action<Exception>? onFailed = null)
    {
        ArgumentNullException.ThrowIfNull(execute);

        _execute = execute;
        _canExecute = canExecute;
        _onFailed = onFailed;
    }

    /// <summary>
    /// 命令的可执行状态发生变化时触发。
    /// </summary>
    /// <remarks>由数据绑定引擎订阅，业务代码不要直接调用。</remarks>
    public event EventHandler? CanExecuteChanged;

    /// <summary>当前是否正在执行。</summary>
    public bool IsRunning => _isRunning;

    /// <inheritdoc />
    public bool CanExecute(object? parameter) => !_isRunning && (_canExecute?.Invoke() ?? true);

    /// <inheritdoc />
    public async void Execute(object? parameter)
    {
        if (_isRunning)
        {
            return;
        }

        _isRunning = true;
        RaiseCanExecuteChanged();

        try
        {
            await _execute();
        }
        catch (Exception ex)
        {
            // 这里的 catch 是必需的，不是偷懒：
            // async void 里逃出去的异常无法被调用方捕获，会直接杀掉进程。
            _onFailed?.Invoke(ex);
        }
        finally
        {
            _isRunning = false;
            RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// 手动通知界面重新评估这个命令能不能执行。
    /// </summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
