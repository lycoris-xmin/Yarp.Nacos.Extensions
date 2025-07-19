using Microsoft.Extensions.Logging;

namespace Lycoris.Yarp.Nacos.Extensions
{
    /// <summary>
    /// 
    /// </summary>
    public interface IYarpLogger
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="logLevel"></param>
        /// <returns></returns>
        bool IsEnabled(LogLevel logLevel);

        /// <summary>
        /// 日志记录
        /// </summary>
        /// <param name="message">日志内容</param>
        void Info(string message);

        /// <summary>
        /// 日志记录
        /// </summary>
        /// <param name="message">日志内容</param>
        void Warn(string message);

        /// <summary>
        /// 日志记录
        /// </summary>
        /// <param name="message">日志内容</param>
        /// <param name="ex"><see cref="Exception"/>异常信息</param>
        void Warn(string message, Exception? ex);

        /// <summary>
        /// 日志记录
        /// </summary>
        /// <param name="message">日志内容</param>
        void Error(string message);

        /// <summary>
        /// 日志记录
        /// </summary>
        /// <param name="message">日志内容</param>
        /// <param name="ex"><see cref="Exception"/></param>
        void Error(string message, Exception? ex);
    }
}
