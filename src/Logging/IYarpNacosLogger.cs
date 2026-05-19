namespace Lycoris.Yarp.Nacos.Extensions.Logging
{
    /// <summary>
    /// 日志记录器接口，用户可实现此接口以自定义日志格式（如日志切割分片）
    /// </summary>
    public interface IYarpNacosLogger
    {
        /// <summary>记录信息日志</summary>
        void Info(string message);
        /// <summary>记录警告日志</summary>
        void Warn(string message);
        /// <summary>记录错误日志</summary>
        void Error(string message, Exception? ex = null);
    }
}
