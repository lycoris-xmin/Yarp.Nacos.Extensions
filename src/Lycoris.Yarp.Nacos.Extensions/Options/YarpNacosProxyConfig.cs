using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;

namespace Lycoris.Yarp.Nacos.Extensions.Options
{
    /// <summary>
    /// 
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
        /// 
        /// </summary>
        public IChangeToken ChangeToken { get; set; } = default!;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="routes"></param>
        /// <param name="clusters"></param>
        public YarpNacosProxyConfig(List<RouteConfig>? routes, List<ClusterConfig>? clusters)
        {
            Routes = routes ?? new List<RouteConfig>();
            Clusters = clusters ?? new List<ClusterConfig>();
        }
    }
}