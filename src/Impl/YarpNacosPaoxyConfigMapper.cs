using Nacos.V2.Naming.Dtos;
using System.Collections.ObjectModel;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Health;
using Yarp.ReverseProxy.LoadBalancing;

namespace Lycoris.Yarp.Nacos.Extensions.Impl
{
    /// <summary>
    /// 默认的 Yarp 代理配置映射器。
    /// 将 Nacos 服务实例映射为 Yarp 路由和集群配置。
    /// 路由规则：/{groupName}/{serviceName}/{**catch-all} → 目标实例地址
    /// 支持继承并覆盖各个方法以实现部分自定义映射逻辑。
    /// </summary>
    public class YarpNacosPaoxyConfigMapper : IYarpNacosPaoxyConfigMapper
    {
        /// <summary>HTTP 协议前缀</summary>
        protected const string HTTP = "http://";
        /// <summary>HTTPS 协议前缀</summary>
        protected const string HTTPS = "https://";
        /// <summary>Nacos 实例元数据中标识是否启用 HTTPS 的 Key</summary>
        protected const string Secure = "secure";
        /// <summary>保留给 Yarp 的元数据 Key 前缀，此前缀的元数据会被传递到 DestinationConfig.Metadata</summary>
        protected const string MetadataPrefix = "yarp";

        /// <summary>
        /// 创建 Yarp 路由匹配规则。
        /// 默认将请求路径 /{groupName}/{serviceName}/{{**catch-all}} 映射到对应集群，
        /// 并移除路径前缀 /{groupName}/{serviceName}
        /// </summary>
        /// <param name="clusterId">集群唯一标识</param>
        /// <param name="groupName">Nacos 服务分组名称</param>
        /// <param name="serviceName">Nacos 服务名称</param>
        /// <returns>Yarp 路由配置</returns>
        public virtual RouteConfig CreateRouteConfig(string clusterId, string groupName, string serviceName)
        {
            return new RouteConfig
            {
                RouteId = $"{clusterId}-route",
                ClusterId = clusterId,
                Match = new RouteMatch
                {
                    Path = $"/{groupName}/{serviceName}/{{**catch-all}}",
                },
                Transforms = new List<Dictionary<string, string>>
                {
                    new Dictionary<string, string>
                    {
                        { "PathRemovePrefix", $"/{groupName}/{serviceName}" }
                    }
                }
            };
        }

        /// <summary>
        /// 创建 Yarp 集群配置。
        /// 默认使用 PowerOfTwoChoices 负载均衡策略，并启用被动健康检查（传输失败率策略，10 分钟后重新激活）
        /// </summary>
        /// <param name="clusterId">集群编号</param>
        /// <param name="destinations">目标实例集合</param>
        /// <returns>Yarp 集群配置</returns>
        public virtual ClusterConfig CreateClusterConfig(string clusterId, IReadOnlyDictionary<string, DestinationConfig> destinations)
        {
            return new ClusterConfig()
            {
                ClusterId = clusterId,
                LoadBalancingPolicy = LoadBalancingPolicies.PowerOfTwoChoices,
                Destinations = destinations,
                HealthCheck = new HealthCheckConfig()
                {
                    Passive = new PassiveHealthCheckConfig()
                    {
                        Enabled = true,
                        Policy = HealthCheckConstants.PassivePolicy.TransportFailureRate,
                        ReactivationPeriod = TimeSpan.FromMinutes(10)
                    }
                }
            };
        }

        /// <summary>
        /// 从 Nacos 实例列表生成 Yarp 目标实例配置。
        /// 仅包含健康且启用的实例，根据实例元数据中的 secure Key 决定使用 http 还是 https，
        /// 并将以 "yarp" 为前缀的元数据传递到目标配置中。
        /// 目标 Key 使用 {Ip}:{Port} 以确保唯一性。
        /// </summary>
        /// <param name="instances">Nacos 实例列表</param>
        /// <returns>目标地址到目标配置的映射</returns>
        public virtual Dictionary<string, DestinationConfig> CreateDestinationConfig(List<Instance> instances)
        {
            var destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase);

            foreach (var instance in instances.Where(x => x.Healthy && x.Enabled))
            {
                var address = instance.Metadata.TryGetValue(Secure, out _) ? $"{HTTPS}{instance.Ip}:{instance.Port}" : $"{HTTP}{instance.Ip}:{instance.Port}";

                // filter the metadata from instance
                var meta = instance.Metadata.Where(x => x.Key.StartsWith(MetadataPrefix, StringComparison.OrdinalIgnoreCase)).ToDictionary(s => s.Key, s => s.Value, StringComparer.OrdinalIgnoreCase);

                // 被动健康检查处理
                meta.TryAdd(TransportFailureRateHealthPolicyOptions.FailureRateLimitMetadataName, "0.5");
                meta.TryAdd(YarpNacosConstants.InstanceWeight, instance.Weight.ToString());

                var metadata = new ReadOnlyDictionary<string, string>(meta ?? new Dictionary<string, string>());

                var destination = new DestinationConfig
                {
                    Address = address,
                    Metadata = metadata
                };

                destinations.Add($"{instance.Ip}:{instance.Port}", destination);
            }

            return destinations;
        }
    }
}
