namespace Lycoris.Yarp.Nacos.Extensions.Logging
{
    /// <summary>
    /// 日志工厂接口，用户可实现此接口以自定义日志输出
    /// </summary>
    public interface IYarpNacosLoggerFactory
    {
        /// <summary>创建指定类型的日志记录器</summary>
        IYarpNacosLogger CreateLogger<T>();
    }
}
