using Nacos.V2.Naming.Dtos;
using Yarp.ReverseProxy.Configuration;

namespace Lycoris.Yarp.Nacos.Extensions
{
    /// <summary>
    /// Yarp 反向代理配置映射器接口。
    /// 实现此接口以自定义从 Nacos 实例到 Yarp 路由/集群配置的映射规则。
    /// </summary>
    public interface IYarpNacosPaoxyConfigMapper
    {
        /// <summary>
        /// 创建 Yarp 路由匹配规则
        /// </summary>
        /// <param name="clusterId">集群唯一标识，由 groupName@@serviceName 组成</param>
        /// <param name="groupName">Nacos 服务分组名称</param>
        /// <param name="serviceName">Nacos 服务名称</param>
        /// <returns>Yarp 路由配置</returns>
        RouteConfig CreateRouteConfig(string clusterId, string groupName, string serviceName);

        /// <summary>
        /// 创建 Yarp 集群配置
        /// </summary>
        /// <param name="clusterId">集群唯一标识</param>
        /// <param name="destinations">目标实例集合</param>
        /// <returns>Yarp 集群配置</returns>
        ClusterConfig CreateClusterConfig(string clusterId, IReadOnlyDictionary<string, DestinationConfig> destinations);

        /// <summary>
        /// 从 Nacos 实例列表生成 Yarp 目标实例配置
        /// </summary>
        /// <param name="instances">Nacos 健康实例列表</param>
        /// <returns>目标地址到目标配置的映射字典</returns>
        Dictionary<string, DestinationConfig> CreateDestinationConfig(List<Instance> instances);
    }
}
