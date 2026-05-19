using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;

namespace Lycoris.Yarp.Nacos.Extensions
{
    /// <summary>
    /// Yarp Nacos 存储接口，负责管理与 Nacos 服务发现集成后的 Yarp 代理配置。
    /// 包括配置获取、缓存管理、服务监听订阅和配置热重载。
    /// </summary>
    public interface IYarpNacosStore
    {
        /// <summary>
        /// 获取当前 Yarp 反向代理配置
        /// </summary>
        /// <returns>包含路由和集群配置的代理配置对象</returns>
        Task<IProxyConfig> GetConfigAsync();

        /// <summary>
        /// 触发配置重载，通知 Yarp 代理配置已更新
        /// </summary>
        void Reload();

        /// <summary>
        /// 获取配置变更令牌，用于监听配置更新
        /// </summary>
        /// <returns>变更令牌</returns>
        IChangeToken GetReloadToken();

        /// <summary>
        /// 获取 Nacos 上指定群组的所有微服务列表
        /// </summary>
        /// <returns>群组名称到服务名称列表的映射</returns>
        Task<Dictionary<string, List<string>>> GetNacosGroupServicesAsync();

        /// <summary>
        /// 为新增的集群服务添加 Nacos 事件监听
        /// </summary>
        /// <param name="groupServices">群组及其服务列表</param>
        Task AddClusterServiceSubscribeAsync(Dictionary<string, List<string>>? groupServices);

        /// <summary>
        /// 移除已下线集群服务的反向代理配置及事件监听
        /// </summary>
        /// <param name="groupServices">需要移除的集群服务标识列表</param>
        Task RemoveClusterProxyConfigAsync(List<string>? groupServices);

        /// <summary>
        /// 为新集群服务创建 Yarp 反向代理规则并订阅变更
        /// </summary>
        /// <param name="groupServices">群组及其服务列表</param>
        Task AddClusterProxyConfigAsync(Dictionary<string, List<string>>? groupServices);

        /// <summary>
        /// 获取当前缓存中所有集群服务的标识列表
        /// </summary>
        /// <returns>集群服务标识（groupId@@serviceName）列表</returns>
        List<string> GetCachedClusterList();
    }
}
