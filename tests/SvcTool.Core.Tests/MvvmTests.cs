using SvcTool.Shared.Mvvm;

namespace SvcTool.Core.Tests;

/// <summary>
/// MVVM 基础设施的测试。
/// </summary>
/// <remarks>
/// 这部分是自己手写的（项目约束是不引第三方库），所以更需要测试兜住。
/// 尤其是"值没变就不发通知"这一条：界面列表如果频繁收到无意义的通知，
/// 会反复重建视觉元素，滚动位置都会被重置。
/// </remarks>
[TestClass]
public sealed class MvvmTests
{
    [TestMethod]
    public void SetProperty_RaisesPropertyChangedWithPropertyName()
    {
        var target = new SampleObservable();
        var raised = new List<string?>();
        target.PropertyChanged += (_, args) => raised.Add(args.PropertyName);

        target.Text = "hello";

        CollectionAssert.AreEqual(new[] { nameof(SampleObservable.Text) }, raised);
    }

    [TestMethod]
    public void SetProperty_WithSameValue_DoesNotRaise()
    {
        var target = new SampleObservable { Text = "hello" };
        var raiseCount = 0;
        target.PropertyChanged += (_, _) => raiseCount++;

        target.Text = "hello";

        Assert.AreEqual(0, raiseCount);
    }

    [TestMethod]
    public void OnPropertyChanged_CanBeRaisedManuallyForDerivedProperties()
    {
        var target = new SampleObservable();
        var raised = new List<string?>();
        target.PropertyChanged += (_, args) => raised.Add(args.PropertyName);

        target.NotifyTextLength();

        CollectionAssert.Contains(raised, nameof(SampleObservable.TextLength));
    }

    [TestMethod]
    public void RelayCommand_Execute_InvokesAction()
    {
        var invoked = false;
        var command = new RelayCommand(() => invoked = true);

        command.Execute(parameter: null);

        Assert.IsTrue(invoked);
    }

    [TestMethod]
    public void RelayCommand_CanExecute_FollowsThePredicate()
    {
        var allowed = false;
        var command = new RelayCommand(() => { }, () => allowed);

        Assert.IsFalse(command.CanExecute(parameter: null));

        allowed = true;

        Assert.IsTrue(command.CanExecute(parameter: null));
    }

    [TestMethod]
    public void RelayCommand_WithoutPredicate_IsAlwaysExecutable()
        => Assert.IsTrue(new RelayCommand(() => { }).CanExecute(parameter: null));

    [TestMethod]
    public async Task AsyncRelayCommand_IsDisabledWhileRunning()
    {
        var release = new TaskCompletionSource();
        var command = new AsyncRelayCommand(() => release.Task);

        Assert.IsTrue(command.CanExecute(parameter: null), "初始状态应当可执行");

        command.Execute(parameter: null);

        // 执行期间必须锁住，否则用户连点会往系统里发出多个冲突请求
        Assert.IsFalse(command.CanExecute(parameter: null), "执行期间应当不可执行");

        release.SetResult();
        await Task.Yield();

        Assert.IsTrue(command.CanExecute(parameter: null), "执行结束后应当恢复可执行");
    }

    [TestMethod]
    public async Task AsyncRelayCommand_ReportsFailureInsteadOfCrashing()
    {
        var captured = new List<Exception>();
        var command = new AsyncRelayCommand(
            () => throw new InvalidOperationException("模拟失败"),
            onFailed: captured.Add);

        command.Execute(parameter: null);
        await Task.Yield();

        Assert.HasCount(1, captured);
        Assert.IsTrue(captured[0] is InvalidOperationException, "应当把原始异常原样交给回调");
    }

    /// <summary>测试用的最小视图模型。</summary>
    private sealed class SampleObservable : ObservableObject
    {
        private string _text = string.Empty;

        public string Text
        {
            get => _text;
            set => SetProperty(ref _text, value);
        }

        public int TextLength => _text.Length;

        public void NotifyTextLength() => OnPropertyChanged(nameof(TextLength));
    }
}
