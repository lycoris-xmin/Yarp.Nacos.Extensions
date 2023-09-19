using Lycoris.Base.Logging;
using Lycoris.Yarp.Nacos.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.LoadBalancing;

namespace Lycoris.Yarp.Nacos.Extensions
{
    /// <summary>
    /// 
    /// </summary>
    public sealed class YarpNacosPaoxyBuilder
    {
        private readonly IServiceCollection services;

        internal Action<IServiceCollection>? CustomeHostedService = null;

        internal Action<IServiceCollection>? LoadBalancingPolicy = null;

        internal Action<YarpNacosOptions> Option { get; private set; }

        internal string? LoadBalancingPolicyName { get; private set; }

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="services"></param>
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
        /// 构建基础配置
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public YarpNacosPaoxyBuilder OptionBuilder(Action<YarpNacosOptions> builder)
        {
            //LoadBalancingPolicies.PowerOfTwoChoices
            this.Option = builder;
            return this;
        }

        /// <summary>
        /// 使用自定义反向代理规则
        /// 自定义反向代理规则需要实现 <see cref="IYarpNacosPaoxyConfigMapper"/>
        /// </summary>
        /// <typeparam name="T"><see cref="IYarpNacosPaoxyConfigMapper"/></typeparam>
        public void AddYarpNacosPaoxyConfigMapper<T>() where T : IYarpNacosPaoxyConfigMapper => this.services.TryAddSingleton(typeof(IYarpNacosPaoxyConfigMapper), typeof(T));

        /// <summary>
        /// 使用自定义状态管理器
        /// 自定义状态管理器需要实现 <see cref="IYarpNacosStore"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void AddYarpNacosStore<T>() where T : IYarpNacosStore => this.services.TryAddSingleton(typeof(IYarpNacosStore), typeof(T));

        /// <summary>
        /// 使用自定义配置创建者
        /// 自定义配置创建者需要实现 <see cref="IProxyConfigProvider"/>
        /// </summary>
        /// <typeparam name="T"><see cref="IProxyConfigProvider"/></typeparam>
        public void AddYarpProxyConfigProvider<T>() where T : IProxyConfigProvider => this.services.TryAddSingleton(typeof(IProxyConfigProvider), typeof(T));

        /// <summary>
        /// 使用自定义日志工厂
        /// 自定义日志工厂需要实现 <see cref="ILycorisLoggerFactory"/> 并配合 <see cref="ILycorisLogger"/> 接口 实现自定义日志记录功能
        /// </summary>
        /// <typeparam name="T"><see cref="ILycorisLoggerFactory"/></typeparam>
        public void AddLycorisLoggerFactory<T>() where T : ILycorisLoggerFactory => this.services.TryAddSingleton(typeof(ILycorisLoggerFactory), typeof(T));

        /// <summary>
        /// 使用自定义服务上下线任务
        /// </summary>
        /// <typeparam name="T"><see cref="IHostedService"/></typeparam>
        public void AddNacosServiceHeart<T>() where T : class, IHostedService => this.CustomeHostedService = (s) => s.AddHostedService<T>();

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void AddLoadBalancingPolicy<T>() where T : class, ILoadBalancingPolicy => this.LoadBalancingPolicy = (s) => s.AddSingleton<ILoadBalancingPolicy, T>();

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void AddLoadBalancingPolicy<T>(string policyName) where T : class, ILoadBalancingPolicy
        {
            this.LoadBalancingPolicyName = policyName;
            this.LoadBalancingPolicy = (s) => s.AddSingleton<ILoadBalancingPolicy, T>();
        }

        /// <summary>
        /// 
        /// </summary>
        public void AddWeightLoadBalancingPolicy()
        {
            this.LoadBalancingPolicyName = YarpNacosConstants.WeightLoadBalancingPolicy;
            this.services.AddSingleton<ILoadBalancingPolicy, WeightLoadBalancingPolicy>();
        }
    }
}
