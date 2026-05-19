using Microsoft.Extensions.Logging;

namespace Lycoris.Yarp.Nacos.Extensions.Logging
{
    /// <summary>
    /// 默认日志工厂实现。基于 <see cref="ILoggerFactory"/> 创建日志记录器，
    /// 将日志输出到 Microsoft.Extensions.Logging 基础设施。
    /// 用户可通过 <see cref="YarpNacosPaoxyBuilder.AddYarpNacosLoggerFactory{T}"/> 替换为自定义实现。
    /// </summary>
    internal sealed class DefaultYarpNacosLoggerFactory : IYarpNacosLoggerFactory
    {
        /// <summary>底层的 MS.Extensions.Logging 日志工厂</summary>
        private readonly ILoggerFactory _factory;

        /// <summary>
        /// 初始化默认日志工厂
        /// </summary>
        /// <param name="factory">MS.Extensions.Logging 的 ILoggerFactory 实例</param>
        public DefaultYarpNacosLoggerFactory(ILoggerFactory factory) => _factory = factory;

        /// <summary>
        /// 创建指定类型名称的日志记录器。
        /// 日志类别名为类型的全名（FullName）。
        /// </summary>
        /// <typeparam name="T">用于确定日志类别名的类型</typeparam>
        /// <returns>日志记录器实例</returns>
        public IYarpNacosLogger CreateLogger<T>() => new DefaultYarpNacosLogger(_factory.CreateLogger<T>());
    }
}
