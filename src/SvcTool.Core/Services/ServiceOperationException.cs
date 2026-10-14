namespace SvcTool.Core.Services;

/// <summary>
/// 服务操作失败时抛出的异常。
/// </summary>
/// <remarks>
/// 与普通的 <see cref="Exception"/> 相比，这里额外保留了 Windows 的原始错误码。
/// 界面层可以直接把 <see cref="Exception.Message"/> 显示给用户（已经是中文说明），
/// 也可以把 <see cref="ErrorCode"/> 附在详情里便于排查。
///
/// 错误码到中文说明的翻译规则见 设计文档.md 第 7 节。
/// </remarks>
public sealed class ServiceOperationException : Exception
{
    /// <summary>初始化一个不含错误码的实例。</summary>
    public ServiceOperationException()
    {
    }

    /// <summary>用指定的错误说明初始化实例。</summary>
    /// <param name="message">错误说明，应当是中文。</param>
    public ServiceOperationException(string message)
        : base(message)
    {
    }

    /// <summary>用指定的错误说明和内部异常初始化实例。</summary>
    /// <param name="message">错误说明，应当是中文。</param>
    /// <param name="innerException">引发失败的内部异常。</param>
    public ServiceOperationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>用指定的错误码和错误说明初始化实例。</summary>
    /// <param name="errorCode">Windows 原始错误码。没有错误码时传 0。</param>
    /// <param name="message">错误说明，应当是中文。</param>
    public ServiceOperationException(int errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    /// <summary>用指定的错误码、错误说明和内部异常初始化实例。</summary>
    /// <param name="errorCode">Windows 原始错误码。没有错误码时传 0。</param>
    /// <param name="message">错误说明，应当是中文。</param>
    /// <param name="innerException">引发失败的内部异常。</param>
    public ServiceOperationException(int errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Windows 的原始错误码，例如 1053 表示服务启动超时。
    /// 没有错误码时为 0。
    /// </summary>
    public int ErrorCode { get; }
}
