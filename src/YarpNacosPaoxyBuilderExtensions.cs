using Lycoris.Yarp.Nacos.Extensions.Impl;
using Lycoris.Yarp.Nacos.Extensions.Logging;
using Lycoris.Yarp.Nacos.Extensions.Options;
using Lycoris.Yarp.Nacos.Extensions.Tracing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nacos.V2;
using Yarp.ReverseProxy.Configuration;

namespace Lycoris.Yarp.Nacos.Extensions
{
    /// <summary>
    /// Yarp Nacos 代理扩展方法，提供向 DI 容器注册 Nacos 扩展的入口
    /// </summary>
    public static class YarpNacosPaoxyBuilderExtensions
    {
        /// <summary>
        /// 添加 Yarp 的 Nacos 扩展，自动从 Nacos 注册中心发现服务并生成反向代理配置。
        /// 同时注册 <see cref="IReverseProxyBuilder"/> 以启用 Yarp 反向代理。
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="configuire">扩展配置委托</param>
        /// <returns>Yarp 反向代理构建器</returns>
        public static IReverseProxyBuilder AddYarpNacosPaoxy(this IServiceCollection services, Action<YarpNacosPaoxyBuilder> configuire) => services.AddReverseProxy().AddNacosDynamicPaoxyConfig(configuire);

        /// <summary>
        /// 添加 Yarp 的 Nacos 扩展，从 <see cref="IConfiguration"/> 绑定基础配置。
        /// 适用于通过 appsettings.json 配置常用选项的场景
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="configuration">配置节，如 <c>configuration.GetSection("YarpNacos")</c></param>
        /// <param name="configuire">可选的额外配置委托，可在配置基础上进行代码覆盖</param>
        /// <returns>Yarp 反向代理构建器</returns>
        public static IReverseProxyBuilder AddYarpNacosPaoxy(this IServiceCollection services, IConfiguration configuration, Action<YarpNacosPaoxyBuilder>? configuire = null)
        {
            return services.AddReverseProxy().AddNacosDynamicPaoxyConfig(configuration, configuire);
        }

        /// <summary>
        /// 向已有的 Yarp 反向代理构建器添加 Nacos 动态配置支持
        /// </summary>
        /// <param name="proxyBuilder">Yarp 反向代理构建器</param>
        /// <param name="configuire">扩展配置委托</param>
        /// <returns>Yarp 反向代理构建器</returns>
        public static IReverseProxyBuilder AddNacosDynamicPaoxyConfig(this IReverseProxyBuilder proxyBuilder, Action<YarpNacosPaoxyBuilder> configuire)
        {
            var buidler = new YarpNacosPaoxyBuilder(proxyBuilder.Services);

            configuire.Invoke(buidler);

            buidler.CustomeHostedService ??= (s) => s.AddHostedService<YarpNacosHostedService>();

            proxyBuilder.Services.Configure<YarpNacosOptions>(opt =>
            {
                buidler.Option.Invoke(opt);

                if (buidler.LoadBalancingPolicy != null && !string.IsNullOrEmpty(buidler.LoadBalancingPolicyName) && string.IsNullOrEmpty(opt.LoadBalancingPolicyName))
                    opt.LoadBalancingPolicyName = buidler.LoadBalancingPolicyName;
            });

            proxyBuilder.Services.TryAddSingleton<IYarpNacosTracing>(NoopYarpNacosTracing.Instance);
            proxyBuilder.Services.AddDefaultLoggerFactory();
            if (buidler.NacosNamespaceClients.Count > 0)
                proxyBuilder.Services.AddSingleton<IReadOnlyDictionary<string, INacosNamingService>>(buidler.NacosNamespaceClients);
            proxyBuilder.Services.AddHttpClient<IYarpNacosHttpClient, YarpNacosHttpClient>();
            proxyBuilder.Services.TryAddSingleton<IYarpNacosPaoxyConfigMapper, YarpNacosPaoxyConfigMapper>();
            proxyBuilder.Services.TryAddSingleton<IYarpNacosStore, YarpNacosStore>();
            proxyBuilder.Services.TryAddSingleton<IProxyConfigProvider, YarpProxyConfigProvider>();
            buidler.CustomeHostedService(proxyBuilder.Services);
            buidler.LoadBalancingPolicy?.Invoke(proxyBuilder.Services);

            return proxyBuilder;
        }

        /// <summary>
        /// 向已有的 Yarp 反向代理构建器添加 Nacos 动态配置支持，从 <see cref="IConfiguration"/> 绑定基础配置
        /// </summary>
        /// <param name="proxyBuilder">Yarp 反向代理构建器</param>
        /// <param name="configuration">配置节</param>
        /// <param name="configuire">可选的额外配置委托</param>
        /// <returns>Yarp 反向代理构建器</returns>
        public static IReverseProxyBuilder AddNacosDynamicPaoxyConfig(this IReverseProxyBuilder proxyBuilder, IConfiguration configuration, Action<YarpNacosPaoxyBuilder>? configuire = null)
        {
            var buidler = new YarpNacosPaoxyBuilder(proxyBuilder.Services);

            buidler.OptionBuilder(opt => configuration.Bind(opt));

            configuire?.Invoke(buidler);

            buidler.CustomeHostedService ??= (s) => s.AddHostedService<YarpNacosHostedService>();

            proxyBuilder.Services.Configure<YarpNacosOptions>(opt =>
            {
                buidler.Option.Invoke(opt);

                if (buidler.LoadBalancingPolicy != null && !string.IsNullOrEmpty(buidler.LoadBalancingPolicyName) && string.IsNullOrEmpty(opt.LoadBalancingPolicyName))
                    opt.LoadBalancingPolicyName = buidler.LoadBalancingPolicyName;
            });

            proxyBuilder.Services.TryAddSingleton<IYarpNacosTracing>(NoopYarpNacosTracing.Instance);
            proxyBuilder.Services.AddDefaultLoggerFactory();
            if (buidler.NacosNamespaceClients.Count > 0)
                proxyBuilder.Services.AddSingleton<IReadOnlyDictionary<string, INacosNamingService>>(buidler.NacosNamespaceClients);
            proxyBuilder.Services.AddHttpClient<IYarpNacosHttpClient, YarpNacosHttpClient>();
            proxyBuilder.Services.TryAddSingleton<IYarpNacosPaoxyConfigMapper, YarpNacosPaoxyConfigMapper>();
            proxyBuilder.Services.TryAddSingleton<IYarpNacosStore, YarpNacosStore>();
            proxyBuilder.Services.TryAddSingleton<IProxyConfigProvider, YarpProxyConfigProvider>();
            buidler.CustomeHostedService(proxyBuilder.Services);
            buidler.LoadBalancingPolicy?.Invoke(proxyBuilder.Services);

            return proxyBuilder;
        }
    }
}
