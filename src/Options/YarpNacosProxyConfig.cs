using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;

namespace Lycoris.Yarp.Nacos.Extensions.Options
{
    /// <summary>
    /// Yarp Nacos 代理配置，实现 <see cref="IProxyConfig"/> 接口。
    /// 包含路由配置列表、集群配置列表和用于通知配置变更的 ChangeToken
    /// </summary>
    public class YarpNacosProxyConfig : IProxyConfig
    {
        /// <summary>
        /// 路由配置列表
        /// </summary>
        public List<RouteConfig> Routes { get; internal set; } = new List<RouteConfig>();

        /// <summary>
        /// 集群配置列表
        /// </summary>
        public List<ClusterConfig> Clusters { get; internal set; } = new List<ClusterConfig>();

        IReadOnlyList<RouteConfig> IProxyConfig.Routes => Routes;

        IReadOnlyList<ClusterConfig> IProxyConfig.Clusters => Clusters;

        /// <summary>
        /// 配置变更令牌，Yarp 通过此令牌感知配置更新
        /// </summary>
        public IChangeToken ChangeToken { get; set; } = default!;

        /// <summary>
        /// 初始化 Yarp Nacos 代理配置
        /// </summary>
        /// <param name="routes">路由配置列表</param>
        /// <param name="clusters">集群配置列表</param>
        public YarpNacosProxyConfig(List<RouteConfig>? routes, List<ClusterConfig>? clusters)
        {
            Routes = routes ?? new List<RouteConfig>();
            Clusters = clusters ?? new List<ClusterConfig>();
        }
    }
}
