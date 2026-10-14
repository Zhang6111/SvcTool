using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using SvcTool.Core.Models;
using SvcTool.Core.Services;
using SvcTool.Shared.Mvvm;

namespace SvcTool.Features.ServiceList;

/// <summary>
/// 服务列表页的视图模型：主界面的全部状态和行为都在这里。
/// </summary>
/// <remarks>
/// 职责边界（见 设计文档.md 第 1.7 节）：
///   - 这一层只做"编排"：调用核心层、维护自己暴露给界面的状态
///   - 真正去和 Windows 打交道的活全在 <see cref="IServiceManager"/> 的实现里
///   - 这里不出现任何 System.Windows.Controls，也不直接操作控件
///
/// 目前接的是假实现（FakeServiceManager），M1 阶段会换成真实实现，
/// 这一层不需要任何改动 —— 这正是接口隔离带来的好处。
/// </remarks>
public sealed class ServiceListViewModel : ObservableObject
{
    private readonly IServiceManager _serviceManager;
    private readonly ObservableCollection<ServiceInfo> _services = [];

    private string _searchText = string.Empty;
    private CategoryFilterOption _selectedCategoryFilter;
    private ServiceInfo? _selectedService;
    private bool _isLoading;
    private string? _errorMessage;

    /// <summary>初始化视图模型。</summary>
    /// <param name="serviceManager">
    /// 服务管理能力的实现。M0/M1 阶段传 <see cref="FakeServiceManager"/>，
    /// 正式版本传真实的实现。
    /// </param>
    public ServiceListViewModel(IServiceManager serviceManager)
    {
        ArgumentNullException.ThrowIfNull(serviceManager);

        _serviceManager = serviceManager;

        CategoryFilters =
        [
            new CategoryFilterOption("全部", null),
            new CategoryFilterOption("驱动", ServiceCategory.Driver),
            new CategoryFilterOption("系统", ServiceCategory.System),
            new CategoryFilterOption("自建", ServiceCategory.User),
        ];
        _selectedCategoryFilter = CategoryFilters[0];

        // 用 WPF 的集合视图做过滤：原始集合不变，视图按条件筛选出可见的部分。
        // 这比自己维护"过滤后的列表"要省事，也不会出现两个集合不同步的问题。
        ServicesView = CollectionViewSource.GetDefaultView(_services);
        ServicesView.Filter = MatchesFilter;

        // 注意这里要包一层 lambda：LoadAsync 带可选参数，
        // 方法组转换不会自动填充默认值，直接传方法名编译不过。
        RefreshCommand = new AsyncRelayCommand(() => LoadAsync(), () => !IsLoading, OnOperationFailed);
        StartCommand = new AsyncRelayCommand(
            () => RunOnSelectionAsync(service => _serviceManager.StartAsync(service.ServiceName)),
            () => CanOperate && SelectedService!.Status != ServiceStatus.Running,
            OnOperationFailed);
        StopCommand = new AsyncRelayCommand(
            () => RunOnSelectionAsync(service => _serviceManager.StopAsync(service.ServiceName)),
            () => CanOperate && SelectedService!.Status == ServiceStatus.Running && SelectedService.CanStop,
            OnOperationFailed);
        RestartCommand = new AsyncRelayCommand(
            () => RunOnSelectionAsync(service => _serviceManager.RestartAsync(service.ServiceName)),
            () => CanOperate && SelectedService!.Status == ServiceStatus.Running && SelectedService.CanStop,
            OnOperationFailed);
    }

    // ================================================================ 属性
    // 暴露给界面绑定的状态。只有这些，界面不读别的东西。

    /// <summary>服务列表（已经过搜索和分类筛选）。界面直接绑这个。</summary>
    public ICollectionView ServicesView { get; }

    /// <summary>分类筛选的可选项。</summary>
    public IReadOnlyList<CategoryFilterOption> CategoryFilters { get; }

    /// <summary>搜索关键字。按显示名和服务名匹配，不区分大小写。</summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                RefreshView();
            }
        }
    }

    /// <summary>当前选中的分类筛选。</summary>
    public CategoryFilterOption SelectedCategoryFilter
    {
        get => _selectedCategoryFilter;
        set
        {
            if (SetProperty(ref _selectedCategoryFilter, value))
            {
                RefreshView();
            }
        }
    }

    /// <summary>列表中当前选中的服务。没有选中时为 null。</summary>
    public ServiceInfo? SelectedService
    {
        get => _selectedService;
        set
        {
            if (SetProperty(ref _selectedService, value))
            {
                RaiseAllCanExecuteChanged();
            }
        }
    }

    /// <summary>是否正在读取服务列表。加载期间界面显示进度指示。</summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                RaiseAllCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// 需要显示给用户的错误说明。没有错误时为 null。
    /// </summary>
    /// <remarks>界面用它来决定底部信息条是否显示。M2 会扩展成带类型的完整信息条。</remarks>
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    /// <summary>当前是否有错误需要显示。</summary>
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    /// <summary>底部状态栏的文字，例如"共 312 个服务 · 已选中 1 个"。</summary>
    public string SummaryText
    {
        get
        {
            var visible = ServicesView.Cast<object>().Count();
            var total = _services.Count;
            var head = visible == total
                ? $"共 {total} 个服务"
                : $"共 {total} 个服务，已筛选出 {visible} 个";

            return SelectedService is null ? head : $"{head} · 已选中 1 个";
        }
    }

    // ================================================================ 命令

    /// <summary>重新读取服务列表。</summary>
    public AsyncRelayCommand RefreshCommand { get; }

    /// <summary>启动选中的服务。</summary>
    public AsyncRelayCommand StartCommand { get; }

    /// <summary>停止选中的服务。</summary>
    public AsyncRelayCommand StopCommand { get; }

    /// <summary>重启选中的服务。</summary>
    public AsyncRelayCommand RestartCommand { get; }

    /// <summary>
    /// 读取服务列表。界面显示后调用一次，刷新按钮也调用它。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示异步等待的任务。</returns>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var services = await _serviceManager.GetAllAsync(cancellationToken);

            _services.Clear();
            foreach (var service in services)
            {
                _services.Add(service);
            }

            RefreshView();
        }
        catch (OperationCanceledException)
        {
            // 用户主动取消，不算错误
        }
        catch (Exception ex)
        {
            OnOperationFailed(ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ================================================================ 内部实现

    /// <summary>
    /// 当前是否允许对选中的服务执行操作。
    /// </summary>
    /// <remarks>
    /// 三种情况不允许：没选中任何服务、正在加载列表、服务处于"正在启动/正在停止"的过渡状态。
    /// 最后一种尤其重要 —— 过渡期间向系统发请求会直接失败。
    /// </remarks>
    private bool CanOperate => !IsLoading && SelectedService is { IsPending: false };

    private async Task RunOnSelectionAsync(Func<ServiceInfo, Task> operation)
    {
        var target = SelectedService;
        if (target is null)
        {
            return;
        }

        ErrorMessage = null;

        try
        {
            await operation(target);
        }
        finally
        {
            // 操作之后重新读一遍，让界面反映系统里的真实状态，
            // 而不是我们自己猜出来的状态。
            var refreshed = await _serviceManager.GetAsync(target.ServiceName);
            if (refreshed is not null)
            {
                var index = _services.IndexOf(target);
                if (index >= 0)
                {
                    _services[index] = refreshed;
                    SelectedService = refreshed;
                }
            }

            RaiseAllCanExecuteChanged();
        }
    }

    private void OnOperationFailed(Exception exception)
    {
        // ServiceOperationException 的 Message 已经是中文说明，可以直接显示。
        // 其它异常保留原始信息，方便排查。
        ErrorMessage = exception is ServiceOperationException
            ? exception.Message
            : $"操作失败：{exception.Message}";
    }

    private bool MatchesFilter(object item)
    {
        if (item is not ServiceInfo service)
        {
            return false;
        }

        // 分类筛选
        if (SelectedCategoryFilter.Category is { } category && service.Category != category)
        {
            return false;
        }

        // 关键字筛选：显示名和服务名都匹配
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }

        var keyword = SearchText.Trim();
        return service.DisplayName.Contains(keyword, StringComparison.OrdinalIgnoreCase)
            || service.ServiceName.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshView()
    {
        ServicesView.Refresh();
        OnPropertyChanged(nameof(SummaryText));
    }

    private void RaiseAllCanExecuteChanged()
    {
        RefreshCommand.RaiseCanExecuteChanged();
        StartCommand.RaiseCanExecuteChanged();
        StopCommand.RaiseCanExecuteChanged();
        RestartCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(SummaryText));
    }
}

/// <summary>
/// 分类筛选的一个可选项。
/// </summary>
/// <param name="Title">显示在下拉框里的文字。</param>
/// <param name="Category">对应的分类；null 表示"全部"。</param>
public sealed record CategoryFilterOption(string Title, ServiceCategory? Category);
