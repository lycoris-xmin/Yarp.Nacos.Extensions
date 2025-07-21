using Lycoris.Yarp.Nacos.Extensions.Impl;
using Lycoris.Yarp.Nacos.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy.Configuration;

namespace Lycoris.Yarp.Nacos.Extensions
{
    /// <summary>
    /// 
    /// </summary>
    public sealed class YarpNacosHostedService : BackgroundService
    {
        private readonly IYarpLogger? _logger;
        private readonly YarpNacosOptions _options;
        private readonly IYarpNacosStore _store;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="provider"></param>
        public YarpNacosHostedService(IServiceProvider provider)
        {
            _logger = provider.GetService<IYarpLoggerFactory>()?.CreateLogger<YarpNacosStore>();

            _options = provider.GetRequiredService<IOptions<YarpNacosOptions>>().Value;
            _store = provider.GetRequiredService<IYarpNacosStore>();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="stoppingToken"></param>
        /// <returns></returns>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var delayTime = _options.NacosServicesHeartbeat * 1000;
            do
            {
                try
                {
                    var realTimeClusters = await GetRealTimeNacosClustersAsync();
                    var cachedClusters = _store.GetCachedClusterList();

                    // 对比出新增的部分
                    var newGroupServices = realTimeClusters.Except(cachedClusters);
                    // 对比出移除的部分
                    var removedGroupServices = cachedClusters.Except(realTimeClusters);

                    // 存在需要处理的集群服务
                    if (newGroupServices != null && newGroupServices.Any() || (removedGroupServices != null && removedGroupServices.Any()))
                    {
                        if (removedGroupServices != null && removedGroupServices.Any())
                            await RemoveOfflineClustersAsync(removedGroupServices);

                        if (newGroupServices != null && newGroupServices.Any())
                        {
                            await AddNewClustersAsync(newGroupServices);

                            _logger?.Info($"new cluster services:{string.Join(",", newGroupServices.Select(x => x.Replace("@@", ".")).ToArray())} listeners added;");

                            // nacos的服务监听有bug，有时候添加了监听器，但是nacos没有推送集群信息，会导致一直在重复的添加监听器
                            // 所以这里需要延迟三秒，确认配置是否更新成功
                            await Task.Delay(3000, stoppingToken);

                            // 延迟确认配置是否重载
                            // 重新获取Store实例
                            realTimeClusters = await GetRealTimeNacosClustersAsync();
                            cachedClusters = _store.GetCachedClusterList();

                            newGroupServices = realTimeClusters.Except(cachedClusters);

                            if (newGroupServices != null && newGroupServices.Any())
                            {
                                _logger?.Warn($"detected that new cluster services configuration was not added correctly:{string.Join(",", [.. newGroupServices.Select(x => x.Replace("@@", "."))])}");

                                await DelayCheckNewClustersAsync(newGroupServices);
                            }
                        }

                        // 配置重载
                        _store.Reload();
                        var configure = await _store.GetConfigAsync();
                        _logger?.Info($"yarp configuration reloaded:{YarpNacosUtils.JsonSerialize(configure?.Clusters ?? new List<ClusterConfig>())}");
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Error("nacos service heartbeat monitoring exception", ex);
                }

                await Task.Delay(delayTime, stoppingToken);
            } while (true);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private async Task<List<string>> GetRealTimeNacosClustersAsync()
        {
            var realTimeServicesDict = await _store.GetNacosGroupServicesAsync();
            var realTimeSet = YarpNacosUtils.BuildServiceSet(realTimeServicesDict);
            return realTimeSet?.ToList() ?? [];
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupServices"></param>
        /// <returns></returns>
        private async Task AddNewClustersAsync(IEnumerable<string> groupServices)
        {
            _logger?.Info($"new microservice cluster online:{string.Join(",", [.. groupServices.Select(x => x.Replace("@@", "."))])}");

            var clusters = new Dictionary<string, List<string>>();

            foreach (var item in groupServices)
            {
                var (group, service) = YarpNacosUtils.GetGroupService(item);

                if (clusters.TryGetValue(group, out List<string>? value))
                    value.Add(service);
                else
                    clusters.Add(group, [service]);
            }

            // 处理新增的微服务集群
            await _store.AddClusterServiceSubscribeAsync(clusters);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupServices"></param>
        /// <returns></returns>
        private async Task DelayCheckNewClustersAsync(IEnumerable<string> groupServices)
        {
            var clusters = new Dictionary<string, List<string>>();

            foreach (var item in groupServices)
            {
                var (group, service) = YarpNacosUtils.GetGroupService(item);

                if (clusters.TryGetValue(group, out List<string>? value))
                    value.Add(service);
                else
                    clusters.Add(group, [service]);
            }

            await _store.AddClusterProxyConfigAsync(clusters);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupServices"></param>
        /// <returns></returns>
        private async Task RemoveOfflineClustersAsync(IEnumerable<string> groupServices)
        {
            _logger?.Warn($"microservice cluster offline:{string.Join(",", groupServices.Select(x => x.Replace("@@", ".")).ToArray())}");

            // 处理移除的微服务集群
            await _store.RemoveClusterProxyConfigAsync(groupServices.ToList());
        }
    }
}
