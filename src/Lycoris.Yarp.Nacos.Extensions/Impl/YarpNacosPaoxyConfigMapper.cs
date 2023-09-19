using Nacos.V2.Naming.Dtos;
using System.Collections.ObjectModel;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Health;
using Yarp.ReverseProxy.LoadBalancing;

namespace Lycoris.Yarp.Nacos.Extensions.Impl
{
    /// <summary>
    /// yarp proxy rule mapper
    /// </summary>
    public sealed class YarpNacosPaoxyConfigMapper : IYarpNacosPaoxyConfigMapper
    {
        private static readonly string HTTP = "http://";
        private static readonly string HTTPS = "https://";
        private static readonly string Secure = "secure";
        private static readonly string MetadataPrefix = "yarp";

        /// <summary>
        /// 创建Yarp路由匹配规则
        /// Create Yarp route matching rules
        /// </summary>
        /// <param name="clusterId"></param>
        /// <param name="groupName"></param>
        /// <param name="serviceName"></param>
        /// <returns></returns>
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
        /// 创建Yarp集群配置
        /// create a Yarp cluster configuration
        /// </summary>
        /// <param name="clusterId">集群编号(clusterId)</param>
        /// <param name="destinations">集群规则(Describes a destination of a cluster)</param>
        /// <returns></returns>
        public ClusterConfig CreateClusterConfig(string clusterId, IReadOnlyDictionary<string, DestinationConfig> destinations)
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
        /// 生成Yarp集群规则
        /// generate Yarp cluster rules
        /// </summary>
        /// <param name="instances">实例列表(instance list)</param>
        /// <returns></returns>
        public Dictionary<string, DestinationConfig> CreateDestinationConfig(List<Instance> instances)
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

                // TODO: how to define the destination's key, the key should not be changed.
                destinations.Add($"{instance.ClusterName}({instance.ServiceName})", destination);
            }

            return destinations;
        }
    }
}