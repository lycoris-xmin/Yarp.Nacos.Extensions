using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;

namespace Lycoris.Yarp.Nacos.Extensions
{
    /// <summary>
    /// 
    /// </summary>
    public interface IYarpNacosStore
    {
        /// <summary>
        /// 获取yarp 反向代理配置规则
        /// get yarp reverse proxy configuration rules
        /// </summary>
        /// <returns></returns>
        Task<IProxyConfig> GetConfigAsync();

        /// <summary>
        /// 重新载入配置
        /// reload configuration
        /// </summary>
        void Reload();

        /// <summary>
        /// 获取重载令牌
        /// get reloadToken
        /// </summary>
        /// <returns></returns>
        IChangeToken GetReloadToken();

        /// <summary>
        /// 获取Nacos上指定群组的所有服务
        /// get all services of the specified group on nacos
        /// </summary>
        /// <returns></returns>
        Task<Dictionary<string, List<string>>> GetNacosGroupServicesAsync();

        /// <summary>
        /// 添加集群服务监听
        /// </summary>
        /// <param name="groupServices"></param>
        /// <returns></returns>
        Task AddClusterServiceSubscribeAsync(Dictionary<string, List<string>>? groupServices);

        /// <summary>
        /// 移除集群服务反向代理配置
        /// </summary>
        /// <param name="groupServices"></param>
        /// <returns></returns>
        Task RemoveClusterProxyConfigAsync(List<string>? groupServices);

        /// <summary>
        /// 添加集群服务yarp反向代理规则
        /// </summary>
        /// <param name="groupServices"></param>
        /// <returns></returns>
        Task AddClusterProxyConfigAsync(Dictionary<string, List<string>>? groupServices);

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        List<string> GetCachedClusterList();
    }
}