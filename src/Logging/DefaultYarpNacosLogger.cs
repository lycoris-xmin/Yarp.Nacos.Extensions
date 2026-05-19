using Microsoft.Extensions.Logging;

namespace Lycoris.Yarp.Nacos.Extensions.Logging
{
    /// <summary>
    /// 默认日志记录器实现。将日志委托给 <see cref="ILogger"/>，输出到 Microsoft.Extensions.Logging 基础设施。
    /// </summary>
    internal sealed class DefaultYarpNacosLogger : IYarpNacosLogger
    {
        /// <summary>底层的 MS.Extensions.Logging 日志记录器</summary>
        private readonly ILogger _logger;

        /// <summary>
        /// 初始化默认日志记录器
        /// </summary>
        /// <param name="logger">MS.Extensions.Logging 的 ILogger 实例</param>
        public DefaultYarpNacosLogger(ILogger logger) => _logger = logger;

        /// <summary>
        /// 记录 Info 级别日志
        /// </summary>
        /// <param name="message">日志消息</param>
        public void Info(string message) => _logger.LogInformation(message);

        /// <summary>
        /// 记录 Warn 级别日志
        /// </summary>
        /// <param name="message">日志消息</param>
        public void Warn(string message) => _logger.LogWarning(message);

        /// <summary>
        /// 记录 Error 级别日志，可附带异常信息
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="ex">可选的异常对象</param>
        public void Error(string message, Exception? ex = null) => _logger.LogError(ex, message);
    }
}
