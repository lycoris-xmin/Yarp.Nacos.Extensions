using Lycoris.Yarp.Nacos.Extensions.Logging;
using Lycoris.Yarp.Nacos.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Nacos.V2;
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

        internal readonly Dictionary<string, INacosNamingService> NacosNamespaceClients = new();

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

        /// <summary>
        /// 使用轮询负载均衡策略（YARP 内置）。
        /// 请求按顺序依次分配到各个健康实例，适用于实例性能相近的场景。
        /// </summary>
        public void UseRoundRobinLoadBalancing() => this.LoadBalancingPolicyName = LoadBalancingPolicies.RoundRobin;

        /// <summary>
        /// 使用两次选择负载均衡策略（YARP 内置，默认）。
        /// 随机选取两个实例并将请求分配给其中连接数较少的那个，兼顾随机性和负载均衡。
        /// </summary>
        public void UsePowerOfTwoChoicesLoadBalancing() => this.LoadBalancingPolicyName = LoadBalancingPolicies.PowerOfTwoChoices;

        /// <summary>
        /// 使用最少请求负载均衡策略（YARP 内置）。
        /// 将请求分配给当前活跃请求数最少的实例，适用于长连接或耗时不均的场景。
        /// </summary>
        public void UseLeastRequestsLoadBalancing() => this.LoadBalancingPolicyName = LoadBalancingPolicies.LeastRequests;

        /// <summary>
        /// 使用随机负载均衡策略（YARP 内置）。
        /// 随机选择一个健康实例，实现简单、无状态。
        /// </summary>
        public void UseRandomLoadBalancing() => this.LoadBalancingPolicyName = LoadBalancingPolicies.Random;

        /// <summary>
        /// 为指定 Namespace 注册独立的 Nacos 命名服务客户端。
        /// 用于不同群组分布在不同 Nacos Namespace 的场景。
        /// </summary>
        /// <param name="namespace">Nacos Namespace ID</param>
        /// <param name="namingService">该 Namespace 对应的 INacosNamingService 实例</param>
        public void AddNacosNamespaceClient(string @namespace, INacosNamingService namingService)
        {
            this.NacosNamespaceClients[@namespace] = namingService;
        }

        /// <summary>
        /// 注册一个服务变更监听器，当 Nacos 服务上线、下线或实例变更时接收回调通知。
        /// 可用于自定义告警、日志记录、指标上报等场景。支持注册多个监听器。
        /// 需要实现 <see cref="IYarpNacosServiceChangeListener"/> 接口。
        /// </summary>
        /// <typeparam name="T">实现 <see cref="IYarpNacosServiceChangeListener"/> 的类型</typeparam>
        public void AddServiceChangeListener<T>() where T : class, IYarpNacosServiceChangeListener
            => this.services.TryAddEnumerable(ServiceDescriptor.Singleton<IYarpNacosServiceChangeListener, T>());

        /// <summary>
        /// 注册一个聚合 API 服务，网关负责接收客户端请求，内部通过 Nacos 发现并调用各个微服务后组装结果返回。
        /// </summary>
        /// <typeparam name="TInterface">API 契约接口，需实现 <see cref="IYarpNacosApiService"/></typeparam>
        /// <typeparam name="TImpl">API 实现类，负责聚合调用微服务</typeparam>
        /// <param name="configure">API 配置选项</param>
        public void AddApi<TInterface, TImpl>(Action<NacosApiOptions>? configure = null) where TInterface : class, IYarpNacosApiService where TImpl : class, TInterface
        {
            var options = new NacosApiOptions();

            configure?.Invoke(options);

            this.services.Configure<NacosApiOptions>(typeof(TInterface).Name, opt =>
            {
                opt.BasePath = options.BasePath;
                opt.ServiceName = options.ServiceName;
                opt.GroupName = options.GroupName;
                opt.TimeoutSeconds = options.TimeoutSeconds;
                opt.RetryCount = options.RetryCount;
            });

            this.services.AddScoped<TInterface, TImpl>();
        }
    }
}
