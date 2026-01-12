using System.Diagnostics.CodeAnalysis;

namespace MoAI.Infra.Exceptions;

/// <summary>
/// 业务异常.
/// </summary>
public class BusinessException : Exception
{
    /// <summary>
    /// 构建消息.
    /// </summary>
    /// <param name="code"></param>
    /// <param name="message"></param>
    /// <returns>错误消息.</returns>
    public static string CreateMessage(int code, string message)
    {
        return $"code: {code},message: {message}";
    }

    /// <summary>
    ///  http 状态码.
    /// </summary>
    public int StatusCode { get; init; } = 500;

    /// <summary>
    /// 错误信息参数.
    /// </summary>
    public IReadOnlyList<object>? Argments { get; init; } = new List<object>();

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessException"/> class.
    /// </summary>
    public BusinessException()
    {
        if (Argments == null)
        {
            Argments = new List<object>();
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessException"/> class.
    /// </summary>
    /// <param name="statusCode"></param>
    /// <param name="message"></param>
    public BusinessException(int statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessException"/> class.
    /// </summary>
    /// <param name="statusCode"></param>
    /// <param name="message"></param>
    /// <param name="argments"></param>
    public BusinessException(int statusCode, string message, params object[] argments)
        : base(message)
    {
        StatusCode = statusCode;

        Argments = argments;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessException"/> class.
    /// </summary>
    /// <param name="format"></param>
    /// <param name="args"></param>
    public BusinessException([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, params object[] args)
        : base(format)
    {
        Argments = args;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessException"/> class.
    /// </summary>
    /// <param name="ex"></param>
    /// <param name="format"></param>
    /// <param name="args"></param>
    public BusinessException(Exception ex, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, params object[] args)
        : base(format, ex)
    {
        Argments = args;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessException"/> class.
    /// </summary>
    /// <param name="message"></param>
    public BusinessException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessException"/> class.
    /// </summary>
    /// <param name="message"></param>
    /// <param name="innerException"></param>
    public BusinessException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Message;
    }
}