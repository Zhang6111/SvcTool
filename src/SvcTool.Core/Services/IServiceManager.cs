using SvcTool.Core.Models;

namespace SvcTool.Core.Services;

/// <summary>
/// 服务管理能力的接口。
/// </summary>
/// <remarks>
/// 这是整个架构里最重要的一条边界。界面层只依赖这个接口，不直接接触任何
/// Windows 接口调用。
///
/// 好处是可以用一个假实现（<see cref="FakeServiceManager"/>）替代真实实现，
/// 从而在不需要管理员权限、不碰任何真实服务的前提下开发界面、编写单元测试。
/// 这一点对本项目格外关键，因为真实的删除服务操作会不可逆地改动系统。
///
/// 创建、修改、删除服务的方法会在 M2 阶段加入 —— 那些操作的参数较多，
/// 等设计到那一步再定签名，避免现在拍脑袋定错了以后返工。
/// </remarks>
public interface IServiceManager
{
    /// <summary>
    /// 读取本机全部服务的信息。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>服务信息列表，顺序与系统返回的顺序一致。</returns>
    /// <remarks>
    /// 实现在后台线程执行，不会阻塞界面。枚举期间系统上的服务列表可能变化，
    /// 实现需要处理"数据量超出预期"的情况。
    /// </remarks>
    Task<IReadOnlyList<ServiceInfo>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 读取单个服务的详细信息。
    /// </summary>
    /// <param name="serviceName">服务的内部名称，例如 Spooler。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>服务信息；服务不存在时返回 null。</returns>
    Task<ServiceInfo?> GetAsync(string serviceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// 启动指定的服务，并等待它真正进入运行状态。
    /// </summary>
    /// <param name="serviceName">服务的内部名称。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示异步等待的任务。</returns>
    /// <exception cref="ServiceOperationException">
    /// 启动失败或等待超时。异常消息已翻译为中文说明。
    /// </exception>
    /// <remarks>
    /// 系统接口调用返回时服务处于"正在启动"，必须轮询确认最终状态，
    /// 最长等待 30 秒。详见 设计文档.md 第 6.1 节。
    /// </remarks>
    Task StartAsync(string serviceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止指定的服务，并等待它真正停止。
    /// </summary>
    /// <param name="serviceName">服务的内部名称。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示异步等待的任务。</returns>
    /// <exception cref="ServiceOperationException">
    /// 停止失败或等待超时。驱动服务无法用此方法停止，会直接抛出该异常。
    /// </exception>
    Task StopAsync(string serviceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// 重启指定的服务：先停止，确认完全停止后再启动。
    /// </summary>
    /// <param name="serviceName">服务的内部名称。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示异步等待的任务。</returns>
    /// <exception cref="ServiceOperationException">停止或启动失败时抛出。</exception>
    /// <remarks>
    /// 实现时必须在停止完全结束之后再调用启动，否则系统会返回
    /// "服务不接受控制请求"。详见 设计文档.md 第 6.2 节。
    /// </remarks>
    Task RestartAsync(string serviceName, CancellationToken cancellationToken = default);
}
