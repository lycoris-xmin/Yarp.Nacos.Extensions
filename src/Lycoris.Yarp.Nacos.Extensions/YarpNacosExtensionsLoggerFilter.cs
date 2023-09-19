namespace Lycoris.Yarp.Nacos.Extensions
{
    /// <summary>
    /// 
    /// </summary>
    public class YarpNacosExtensionsLoggerFilter
    {
        /// <summary>
        /// 类库的日志过滤
        /// </summary>
        public static List<string> Namespace
        {
            get
            {
                return new List<string>()
                {
                    "Lycoris.Yarp.Nacos.Extensions.Impl.YarpNacosStore",
                    "Lycoris.Yarp.Nacos.Extensions.YarpNacosHostedService"
                };
            }
        }
    }
}
