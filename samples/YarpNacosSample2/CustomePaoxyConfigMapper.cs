using Lycoris.Yarp.Nacos.Extensions;
using Nacos.V2.Naming.Dtos;
using System.Collections.ObjectModel;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Health;
using Yarp.ReverseProxy.LoadBalancing;

namespace YarpNacosSample2
{
    /// <summary>
    /// 自定义代理配置映射器示例。
    /// 演示如何实现 <see cref="IYarpNacosPaoxyConfigMapper"/> 接口来自定义路由和集群的生成规则。
    /// 此处使用 RoundRobin 负载均衡策略，仅过滤健康实例。
    /// </summary>
    public class CustomePaoxyConfigMapper : IYarpNacosPaoxyConfigMapper
    {
        private static readonly string HTTP = "http://";
        private static readonly string HTTPS = "https://";
        private static readonly string Secure = "secure";
        private static readonly string MetadataPrefix = "yarp";

        /// <summary>
        /// 自定义路由匹配规则
        /// </summary>
        public RouteConfig CreateRouteConfig(string clusterId, string groupName, string serviceName)
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
        /// 自定义集群配置：使用 RoundRobin 和被动健康检查
        /// </summary>
        public ClusterConfig CreateClusterConfig(string clusterId, IReadOnlyDictionary<string, DestinationConfig> destinations)
        {
            return new ClusterConfig()
            {
                ClusterId = clusterId,
                LoadBalancingPolicy = LoadBalancingPolicies.RoundRobin,
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
        /// 自定义目标实例配置生成：仅包含健康实例
        /// </summary>
        public Dictionary<string, DestinationConfig> CreateDestinationConfig(List<Instance> instances)
        {
            var destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase);

            foreach (var instance in instances.Where(x => x.Healthy))
            {
                var address = instance.Metadata.TryGetValue(Secure, out _) ? $"{HTTPS}{instance.Ip}:{instance.Port}" : $"{HTTP}{instance.Ip}:{instance.Port}";

                var meta = instance.Metadata.Where(x => x.Key.StartsWith(MetadataPrefix, StringComparison.OrdinalIgnoreCase)).ToDictionary(s => s.Key, s => s.Value, StringComparer.OrdinalIgnoreCase);

                meta.TryAdd(TransportFailureRateHealthPolicyOptions.FailureRateLimitMetadataName, "0.5");

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
