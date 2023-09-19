using Lycoris.Base.Extensions;
using Lycoris.Base.Logging;
using Lycoris.Yarp.Nacos.Extensions.Impl;
using Lycoris.Yarp.Nacos.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Yarp.ReverseProxy.Configuration;

namespace Lycoris.Yarp.Nacos.Extensions
{
    /// <summary>
    /// 
    /// </summary>
    public static class YarpNacosPaoxyBuilderExtensions
    {
        /// <summary>
        /// 添加Yarp的Nacos扩展
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configuire"></param>
        /// <returns></returns>
        public static IReverseProxyBuilder AddYarpNacosPaoxy(this IServiceCollection services, Action<YarpNacosPaoxyBuilder> configuire) => services.AddReverseProxy().AddNacosDynamicPaoxyConfig(configuire);

        /// <summary>
        /// 添加Yarp的Nacos扩展
        /// </summary>
        /// <param name="proxyBuilder"></param>
        /// <param name="configuire"></param>
        /// <returns></returns>
        public static IReverseProxyBuilder AddNacosDynamicPaoxyConfig(this IReverseProxyBuilder proxyBuilder, Action<YarpNacosPaoxyBuilder> configuire)
        {
            var buidler = new YarpNacosPaoxyBuilder(proxyBuilder.Services);

            configuire.Invoke(buidler);

            buidler.CustomeHostedService ??= (s) => s.AddHostedService<YarpNacosHostedService>();

            proxyBuilder.Services.Configure<YarpNacosOptions>(opt =>
            {
                buidler.Option.Invoke(opt);

                if (buidler.LoadBalancingPolicy != null && !buidler.LoadBalancingPolicyName.IsNullOrEmpty() && opt.LoadBalancingPolicyName.IsNullOrEmpty())
                    opt.LoadBalancingPolicyName = buidler.LoadBalancingPolicyName;
            });

            proxyBuilder.Services.AddDefaultLoggerFactory();
            proxyBuilder.Services.TryAddSingleton<IYarpNacosPaoxyConfigMapper, YarpNacosPaoxyConfigMapper>();
            proxyBuilder.Services.TryAddSingleton<IYarpNacosStore, YarpNacosStore>();
            proxyBuilder.Services.TryAddSingleton<IProxyConfigProvider, YarpProxyConfigProvider>();
            buidler.CustomeHostedService(proxyBuilder.Services);
            buidler.LoadBalancingPolicy?.Invoke(proxyBuilder.Services);

            return proxyBuilder;
        }
    }
}
