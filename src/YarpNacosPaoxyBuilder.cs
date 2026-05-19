using Lycoris.Yarp.Nacos.Extensions.Logging;
using Lycoris.Yarp.Nacos.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.LoadBalancing;

namespace Lycoris.Yarp.Nacos.Extensions
{
    /// <summary>
    /// Yarp Nacos 代理构建器，提供流式 API 配置 Nacos 扩展的各项组件。
    /// 支持自定义配置映射器、状态管理器、代理配置提供者、日志工厂、负载均衡策略等。
    /// </summary>
    public sealed class YarpNacosPaoxyBuilder
    {
        private readonly IServiceCollection services;

        internal Action<IServiceCollection>? CustomeHostedService = null;

        internal Action<IServiceCollection>? LoadBalancingPolicy = null;

        /// <summary>
        /// 当前配置选项操作委托
        /// </summary>
        internal Action<YarpNacosOptions> Option { get; private set; }

        /// <summary>
        /// 当前负载均衡策略名称
        /// </summary>
        internal string? LoadBalancingPolicyName { get; private set; }

        /// <summary>
        /// 初始化构建器
        /// </summary>
        /// <param name="services">服务集合</param>
        public YarpNacosPaoxyBuilder(IServiceCollection services)
        {
            this.services = services;
            this.Option = (opt) =>
            {
                opt.GroupNameList = new List<string>();
                opt.PreCount = 50;
            };
        }

        /// <summary>
        /// 配置基础选项，如群组列表、心跳间隔等。
        /// 多次调用会覆盖之前的设置
        /// </summary>
        /// <param name="builder">选项配置委托</param>
        /// <returns>当前构建器实例，支持链式调用</returns>
        public YarpNacosPaoxyBuilder OptionBuilder(Action<YarpNacosOptions> builder)
        {
            this.Option = builder;
            return this;
        }

        /// <summary>
        /// 使用自定义反向代理配置映射器。
        /// 需要实现 <see cref="IYarpNacosPaoxyConfigMapper"/> 接口
        /// </summary>
        /// <typeparam name="T">实现 <see cref="IYarpNacosPaoxyConfigMapper"/> 的类型</typeparam>
        public void AddYarpNacosPaoxyConfigMapper<T>() where T : IYarpNacosPaoxyConfigMapper => this.services.TryAddSingleton(typeof(IYarpNacosPaoxyConfigMapper), typeof(T));

        /// <summary>
        /// 使用自定义状态管理器。
        /// 需要实现 <see cref="IYarpNacosStore"/> 接口
        /// </summary>
        /// <typeparam name="T">实现 <see cref="IYarpNacosStore"/> 的类型</typeparam>
        public void AddYarpNacosStore<T>() where T : IYarpNacosStore => this.services.TryAddSingleton(typeof(IYarpNacosStore), typeof(T));

        /// <summary>
        /// 使用自定义 Yarp 代理配置提供者。
        /// 需要实现 <see cref="IProxyConfigProvider"/> 接口
        /// </summary>
        /// <typeparam name="T">实现 <see cref="IProxyConfigProvider"/> 的类型</typeparam>
        public void AddYarpProxyConfigProvider<T>() where T : IProxyConfigProvider => this.services.TryAddSingleton(typeof(IProxyConfigProvider), typeof(T));

        /// <summary>
        /// 使用自定义日志工厂，用于格式化输出或对接日志切割分片系统。
        /// 需要实现 <see cref="IYarpNacosLoggerFactory"/> 并配合 <see cref="IYarpNacosLogger"/> 接口实现自定义日志记录。
        /// 未设置时默认使用 <c>Microsoft.Extensions.Logging</c> 作为底层实现。
        /// </summary>
        /// <typeparam name="T">实现 <see cref="IYarpNacosLoggerFactory"/> 的类型</typeparam>
        public void AddYarpNacosLoggerFactory<T>() where T : IYarpNacosLoggerFactory => this.services.TryAddSingleton(typeof(IYarpNacosLoggerFactory), typeof(T));

        /// <summary>
        /// 使用自定义服务上下线心跳任务。
        /// 需要继承 <see cref="BackgroundService"/> 或实现 <see cref="IHostedService"/>
        /// </summary>
        /// <typeparam name="T">实现 <see cref="IHostedService"/> 的类型</typeparam>
        public void AddNacosServiceHeart<T>() where T : class, IHostedService => this.CustomeHostedService = (s) => s.AddHostedService<T>();

        /// <summary>
        /// 注册自定义负载均衡策略
        /// </summary>
        /// <typeparam name="T">实现 <see cref="ILoadBalancingPolicy"/> 的类型</typeparam>
        public void AddLoadBalancingPolicy<T>() where T : class, ILoadBalancingPolicy => this.LoadBalancingPolicy = (s) => s.AddSingleton<ILoadBalancingPolicy, T>();

        /// <summary>
        /// 注册自定义负载均衡策略并指定策略名称，用于在 Yarp 集群配置中引用
        /// </summary>
        /// <typeparam name="T">实现 <see cref="ILoadBalancingPolicy"/> 的类型</typeparam>
        /// <param name="policyName">负载均衡策略名称</param>
        public void AddLoadBalancingPolicy<T>(string policyName) where T : class, ILoadBalancingPolicy
        {
            this.LoadBalancingPolicyName = policyName;
            this.LoadBalancingPolicy = (s) => s.AddSingleton<ILoadBalancingPolicy, T>();
        }

        /// <summary>
        /// 启用基于 Nacos 实例权重的负载均衡策略。
        /// 根据 Nacos 实例的 Weight 元数据按权重比例分配请求
        /// </summary>
        public void AddWeightLoadBalancingPolicy()
        {
            this.LoadBalancingPolicyName = YarpNacosConstants.WeightLoadBalancingPolicy;
            this.LoadBalancingPolicy = (s) => s.AddSingleton<ILoadBalancingPolicy, WeightLoadBalancingPolicy>();
        }
    }
}
