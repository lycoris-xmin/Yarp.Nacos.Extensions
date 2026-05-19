using Lycoris.Yarp.Nacos.Extensions.Impl;
using Lycoris.Yarp.Nacos.Extensions.Logging;
using Lycoris.Yarp.Nacos.Extensions.Options;
using Nacos.V2.Naming.Dtos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy.Configuration;

namespace Lycoris.Yarp.Nacos.Extensions
{
    /// <summary>
    /// Nacos 服务心跳后台服务。定期轮询 Nacos 注册中心，检测微服务的上下线变化，
    /// 并自动更新 Yarp 反向代理配置。
    /// </summary>
    public sealed class YarpNacosHostedService : BackgroundService
    {
        /// <summary>
        /// 日志记录器
        /// </summary>
        private readonly IYarpNacosLogger _logger;

        /// <summary>
        /// 扩展配置选项
        /// </summary>
        private readonly YarpNacosOptions _options;

        /// <summary>
        /// Nacos 状态管理器
        /// </summary>
        private IYarpNacosStore _store;

        /// <summary>
        /// 服务变更监听器集合
        /// </summary>
        private readonly IReadOnlyList<IYarpNacosServiceChangeListener> _serviceChangeListeners;

        /// <summary>
        /// 初始化 Nacos 心跳服务
        /// </summary>
        /// <param name="factory">日志工厂</param>
        /// <param name="store">Nacos 状态管理器</param>
        /// <param name="options">扩展配置选项</param>
        /// <param name="serviceChangeListeners">服务变更监听器集合</param>
        public YarpNacosHostedService(IYarpNacosLoggerFactory factory, IYarpNacosStore store, IOptions<YarpNacosOptions> options, IEnumerable<IYarpNacosServiceChangeListener>? serviceChangeListeners = null)
        {
            _logger = factory.CreateLogger<YarpNacosHostedService>();
            _options = options.Value;
            _store = store;
            _serviceChangeListeners = serviceChangeListeners?.ToList() ?? new List<IYarpNacosServiceChangeListener>();
        }

        /// <summary>
        /// 执行心跳循环，定期检测服务上下线并更新 Yarp 配置。
        /// 流程：拉取实时服务列表 → 对比当前缓存 → 新增/移除集群 → 延迟确认 → 配置重载。
        /// </summary>
        /// <param name="stoppingToken">取消令牌</param>
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
                            _logger.Info($"new cluster services:{string.Join(",", newGroupServices.Select(x => x.Replace("@@", ".")).ToArray())} listeners added;");

                            // nacos的服务监听有bug，有时候添加了监听器，但是nacos没有推送集群信息，会导致一直在重复的添加监听器
                            // 所以这里需要延迟三秒，确认配置是否更新成功
                            await Task.Delay(3000, stoppingToken);

                            // 延迟确认配置是否重载，重新获取实时列表进行二次验证
                            realTimeClusters = await GetRealTimeNacosClustersAsync();
                            cachedClusters = _store.GetCachedClusterList();

                            newGroupServices = realTimeClusters.Except(cachedClusters);

                            if (newGroupServices != null && newGroupServices.Any())
                            {
                                _logger.Warn($"detected that new cluster services configuration was not added correctly:{string.Join(",", newGroupServices.Select(x => x.Replace("@@", ".")).ToArray())}");
                                await DelayCheckNewClustersAsync(newGroupServices);
                            }
                        }

                        // 配置重载
                        _store.Reload();
                        var configure = await _store.GetConfigAsync();
                        _logger.Info($"yarp configuration reloaded:{YarpNacosUtils.JsonSerialize(configure?.Clusters ?? new List<ClusterConfig>())}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("nacos service heartbeat monitoring exception", ex);
                }

                await Task.Delay(delayTime, stoppingToken);
            } while (!stoppingToken.IsCancellationRequested);
        }

        /// <summary>
        /// 从 Nacos 获取实时的集群服务标识列表
        /// </summary>
        /// <returns>集群服务标识 (groupId@@serviceName) 列表</returns>
        private async Task<List<string>> GetRealTimeNacosClustersAsync()
        {
            var realTimeServicesDict = await _store.GetNacosGroupServicesAsync();
            var realTimeSet = YarpNacosUtils.BuildServiceSet(realTimeServicesDict);
            return realTimeSet?.ToList() ?? new List<string>();
        }

        /// <summary>
        /// 处理新增的微服务集群，添加 Nacos 事件监听
        /// </summary>
        /// <param name="groupServices">新增的集群服务标识集合</param>
        private async Task AddNewClustersAsync(IEnumerable<string> groupServices)
        {
            _logger.Info($"new microservice cluster online:{string.Join(",", groupServices.Select(x => x.Replace("@@", ".")).ToArray())}");

            var clusters = GroupServicesByGroup(groupServices);
            await _store.AddClusterServiceSubscribeAsync(clusters);

            // 通知监听器：服务上线
            foreach (var item in groupServices)
            {
                var (group, service) = YarpNacosUtils.GetGroupService(item);
                _ = NotifyOnlineAsync(group, service);
            }
        }

        /// <summary>
        /// 延迟确认配置：Nacos 监听未正常推送时，跳过监听步骤直接拉取配置创建代理规则
        /// </summary>
        /// <param name="groupServices">未正确添加的集群服务标识集合</param>
        private async Task DelayCheckNewClustersAsync(IEnumerable<string> groupServices)
        {
            var clusters = GroupServicesByGroup(groupServices);
            await _store.AddClusterProxyConfigAsync(clusters);
        }

        /// <summary>
        /// 将集群服务标识列表按群组归类
        /// </summary>
        /// <param name="groupServices">集群服务标识 (groupId@@serviceName) 集合</param>
        /// <returns>群组名到服务名列表的映射字典</returns>
        private static Dictionary<string, List<string>> GroupServicesByGroup(IEnumerable<string> groupServices)
        {
            var clusters = new Dictionary<string, List<string>>();
            foreach (var item in groupServices)
            {
                var (group, service) = YarpNacosUtils.GetGroupService(item);
                if (clusters.TryGetValue(group, out List<string>? value))
                    value.Add(service);
                else
                    clusters.Add(group, new List<string> { service });
            }
            return clusters;
        }

        /// <summary>
        /// 移除已下线的微服务集群代理配置
        /// </summary>
        /// <param name="groupServices">已下线的集群服务标识集合</param>
        private async Task RemoveOfflineClustersAsync(IEnumerable<string> groupServices)
        {
            _logger.Warn($"microservice cluster offline:{string.Join(",", groupServices.Select(x => x.Replace("@@", ".")).ToArray())}");

            await _store.RemoveClusterProxyConfigAsync(groupServices.ToList());

            // 通知监听器：服务下线
            foreach (var item in groupServices)
            {
                var (group, service) = YarpNacosUtils.GetGroupService(item);
                _ = NotifyOfflineAsync(group, service);
            }
        }

        /// <summary>
        /// 通知所有已注册的监听器：服务上线（fire-and-forget）
        /// </summary>
        private async Task NotifyOnlineAsync(string groupName, string serviceName)
        {
            foreach (var listener in _serviceChangeListeners)
            {
                try
                {
                    await listener.OnServiceOnlineAsync(groupName, serviceName, new List<Instance>(), CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.Error($"service change listener error for online: {groupName}/{serviceName}", ex);
                }
            }
        }

        /// <summary>
        /// 通知所有已注册的监听器：服务下线（fire-and-forget）
        /// </summary>
        private async Task NotifyOfflineAsync(string groupName, string serviceName)
        {
            foreach (var listener in _serviceChangeListeners)
            {
                try
                {
                    await listener.OnServiceOfflineAsync(groupName, serviceName, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.Error($"service change listener error for offline: {groupName}/{serviceName}", ex);
                }
            }
        }
    }
}
