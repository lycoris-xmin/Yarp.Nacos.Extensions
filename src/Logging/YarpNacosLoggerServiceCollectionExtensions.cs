using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Lycoris.Yarp.Nacos.Extensions.Logging
{
    /// <summary>
    /// 日志服务注册扩展
    /// </summary>
    public static class YarpNacosLoggerServiceCollectionExtensions
    {
        /// <summary>
        /// 注册默认日志工厂，使用 Microsoft.Extensions.Logging 作为底层实现
        /// </summary>
        public static IServiceCollection AddDefaultLoggerFactory(this IServiceCollection services)
        {
            services.TryAddSingleton<IYarpNacosLoggerFactory, DefaultYarpNacosLoggerFactory>();
            return services;
        }
    }
}
