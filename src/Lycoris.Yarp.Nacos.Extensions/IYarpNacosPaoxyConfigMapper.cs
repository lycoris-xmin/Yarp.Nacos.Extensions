using Nacos.V2.Naming.Dtos;
using Yarp.ReverseProxy.Configuration;

namespace Lycoris.Yarp.Nacos.Extensions
{
    /// <summary>
    /// 
    /// </summary>
    public interface IYarpNacosPaoxyConfigMapper
    {
        /// <summary>
        /// 创建Yarp路由匹配规则
        /// Create Yarp route matching rules
        /// </summary>
        /// <param name="clusterId">集群唯一标识(clusterId)</param>
        /// <param name="groupName">集群分组名称</param>
        /// <param name="serviceName">集群名称</param>
        /// <returns><see cref="RouteConfig"/></returns>
        RouteConfig CreateRouteConfig(string clusterId, string groupName, string serviceName);

        /// <summary>
        /// 创建Yarp集群配置
        /// create a Yarp cluster configuration
        /// </summary>
        /// <param name="clusterId">集群唯一标识(clusterId)</param>
        /// <param name="destinations">集群规则(Describes a destination of a cluster)</param>
        /// <returns><see cref="ClusterConfig"/></returns>
        ClusterConfig CreateClusterConfig(string clusterId, IReadOnlyDictionary<string, DestinationConfig> destinations);

        /// <summary>
        /// 生成Yarp集群规则
        /// generate Yarp cluster rules
        /// </summary>
        /// <param name="instances">实例列表(instance list)</param>
        /// <returns></returns>
        Dictionary<string, DestinationConfig> CreateDestinationConfig(List<Instance> instances);
    }
}