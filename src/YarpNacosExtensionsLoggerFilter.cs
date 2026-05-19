namespace Lycoris.Yarp.Nacos.Extensions
{
    /// <summary>
    /// 扩展库的日志过滤配置，用于指定需要过滤日志输出的命名空间列表
    /// </summary>
    public class YarpNacosExtensionsLoggerFilter
    {
        /// <summary>
        /// 需要进行日志过滤的命名空间列表
        /// </summary>
        public static readonly List<string> Namespace = new()
        {
            "Lycoris.Yarp.Nacos.Extensions.Impl.YarpNacosStore",
            "Lycoris.Yarp.Nacos.Extensions.YarpNacosHostedService"
        };
    }
}
