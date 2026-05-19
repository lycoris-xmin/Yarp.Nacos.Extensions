using Lycoris.Yarp.Nacos.Extensions.Logging;
using Lycoris.Yarp.Nacos.Extensions.Options;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Nacos.V2;
using Nacos.V2.Naming.Event;
using System.Collections.Concurrent;
using Yarp.ReverseProxy.Configuration;

namespace Lycoris.Yarp.Nacos.Extensions.Impl
{
    /// <summary>
    /// Nacos 状态管理器。负责与 Nacos 服务发现交互，管理 Yarp 代理配置的缓存、
    /// 服务订阅/取消订阅以及配置的热重载。
    /// </summary>
    public sealed class YarpNacosStore : IYarpNacosStore
    {
        private YarpNacosReloadToken _reloadToken = new();
        private readonly IYarpNacosLogger _logger;
        private readonly YarpNacosOptions _options;
        private readonly INacosNamingService _nameSvc;
        private readonly IYarpNacosPaoxyConfigMapper _configMapper;

        private readonly ConcurrentDictionary<string, DateTime> CachedServices = new();
        private readonly ConcurrentDictionary<string, RouteConfig> CachedRoutes = new();
        private readonly ConcurrentDictionary<string, ClusterConfig> CachedClusters = new();

        private readonly ConcurrentDictionary<string, ServiceChangeEventListener> Listener = new();

        /// <summary>
        /// 初始化 Nacos 状态管理器
        /// </summary>
        /// <param name="factory">日志工厂</param>
        /// <param name="optionsAccs">扩展配置选项</param>
        /// <param name="nameSvc">Nacos 命名服务</param>
        /// <param name="configMapper">配置映射器</param>
        public YarpNacosStore(IYarpNacosLoggerFactory factory,
                              IOptions<YarpNacosOptions> optionsAccs,
                              INacosNamingService nameSvc,
                              IYarpNacosPaoxyConfigMapper configMapper)
        {
            _logger = factory.CreateLogger<YarpNacosStore>();
            _options = optionsAccs.Value;
            _nameSvc = nameSvc;
            _configMapper = configMapper;
        }

        /// <summary>
        /// 获取配置重载令牌
        /// </summary>
        public IChangeToken GetReloadToken() => _reloadToken;

        /// <summary>
        /// 触发配置重载，创建新的重载令牌并取消旧令牌
        /// </summary>
        public void Reload() => Interlocked.Exchange(ref _reloadToken, new YarpNacosReloadToken()).OnReload();

        /// <summary>
        /// 获取当前 Yarp 反向代理配置。优先返回缓存，缓存为空时从 Nacos 加载
        /// </summary>
        public async Task<IProxyConfig> GetConfigAsync()
        {
            YarpNacosProxyConfig? proxyConfig;
            if (CachedClusters.Any() && CachedRoutes.Any())
                proxyConfig = new YarpNacosProxyConfig(CachedRoutes.Values.ToList(), CachedClusters.Values.ToList());
            else
            {
                var groupServices = await GetNacosGroupServicesAsync().ConfigureAwait(false);
                proxyConfig = await CreateNacosProxyConfigAsync(groupServices);
            }

            if (proxyConfig == null)
                throw new ArgumentNullException("could not get available yarp configuration");

            return proxyConfig;
        }

        /// <summary>
        /// 获取 Nacos 上配置的所有群组的微服务列表，支持翻页
        /// </summary>
        public async Task<Dictionary<string, List<string>>> GetNacosGroupServicesAsync()
        {
            var groupServicesDict = new Dictionary<string, List<string>>();

            foreach (var groupName in _options.GroupNameList)
            {
                try
                {
                    int pageIndex = 1;

                    var listView = await _nameSvc.GetServicesOfServer(pageIndex, _options.PreCount, groupName).ConfigureAwait(false);

                    if (listView.Count == 0)
                    {
                        groupServicesDict.Add(groupName, new List<string>());
                        continue;
                    }

                    var groupServices = listView.Data;

                    // 如果总数大于当前数量则继续翻页取出所有实例
                    if (listView.Count > _options.PreCount)
                    {
                        do
                        {
                            pageIndex++;
                            var tmp = await _nameSvc.GetServicesOfServer(pageIndex, _options.PreCount, groupName).ConfigureAwait(false);
                            if (tmp.Data == null || tmp.Data.Count == 0)
                                break;
                            groupServices.AddRange(tmp.Data);
                        }
                        while (groupServices.Count < listView.Count);
                    }

                    groupServicesDict.Add(groupName, groupServices);
                }
                catch (Exception ex)
                {
                    _logger?.Error($"load service from nacos service group：{groupName}) failed", ex);
                }
            }

            return groupServicesDict;
        }

        /// <summary>
        /// 为新增的集群服务添加 Nacos 事件监听。
        /// 添加监听后 Nacos 会立即推送一次服务信息，配置的更新由监听事件处理
        /// </summary>
        public async Task AddClusterServiceSubscribeAsync(Dictionary<string, List<string>>? clusterServices)
        {
            if (clusterServices == null || !clusterServices.Any())
                return;

            foreach (var cluster in clusterServices)
            {
                var groupName = cluster.Key;

                foreach (var service in cluster.Value)
                {
                    try
                    {
                        var clusterId = YarpNacosUtils.CreateClusterId(groupName, service);
                        var eventListener = Listener.GetOrAdd(clusterId, _ => new ServiceChangeEventListener(_logger!, this));
                        await _nameSvc.Subscribe(service, groupName, eventListener).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger?.Error($"subscribe nacos service linterer：{groupName}.{service} failed", ex);
                    }
                }
            }
        }

        /// <summary>
        /// 移除已下线集群服务的代理配置，清理缓存并取消 Nacos 事件监听
        /// </summary>
        public async Task RemoveClusterProxyConfigAsync(List<string>? groupServices)
        {
            if (groupServices == null || !groupServices.Any())
                return;

            foreach (var item in groupServices)
            {
                CachedServices.Remove(item, out _);
                CachedClusters.Remove(item, out _);
                CachedRoutes.Remove(item, out _);

                var (group, service) = YarpNacosUtils.GetGroupService(item);
                if (Listener.TryRemove(item, out var eventListener))
                    await _nameSvc.Unsubscribe(service, group, eventListener).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 为集群服务创建 Yarp 反向代理规则，重新订阅 Nacos 事件监听
        /// </summary>
        public async Task AddClusterProxyConfigAsync(Dictionary<string, List<string>>? groupServices)
        {
            if (groupServices == null || !groupServices.Any())
                return;

            foreach (var item in groupServices)
            {
                var group = item.Key;
                foreach (var service in item.Value)
                {
                    var clusterId = YarpNacosUtils.CreateClusterId(group, service);
                    CachedServices[clusterId] = DateTime.Now;

                    try
                    {
                        // 移除原有的事件监听
                        if (Listener.TryRemove(clusterId, out var oldListener))
                            await _nameSvc.Unsubscribe(service, group, oldListener).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger?.Error($"remove the cluster:{group}.{service} linterer failed", ex);
                        continue;
                    }

                    try
                    {
                        // ClusterConfig
                        var instances = await _nameSvc.GetAllInstances(service, group, false).ConfigureAwait(false);
                        var cluster = _configMapper.CreateClusterConfig(clusterId, _configMapper.CreateDestinationConfig(instances));
                        CachedClusters[clusterId] = cluster;

                        // RouteConfig
                        var route = _configMapper.CreateRouteConfig(clusterId, group, service);
                        CachedRoutes[clusterId] = route;

                        // 添加新的事件监听
                        var eventListener = Listener.GetOrAdd(clusterId, _ => new ServiceChangeEventListener(_logger!, this));
                        await _nameSvc.Subscribe(service, group, eventListener).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger?.Error($"add yarp cluster:{group}.{service} configuration failed", ex);
                    }
                }
            }
        }

        /// <summary>
        /// 创建 Yarp 代理配置：拉取所有 Nacos 服务信息并生成路由和集群配置
        /// </summary>
        public async Task<YarpNacosProxyConfig> CreateNacosProxyConfigAsync(Dictionary<string, List<string>> groupServices)
        {
            var clusters = new Dictionary<string, ClusterConfig>();
            var routes = new Dictionary<string, RouteConfig>();

            foreach (var item in groupServices)
            {
                var group = item.Key;

                foreach (var service in item.Value)
                {
                    try
                    {
                        var clusterId = YarpNacosUtils.CreateClusterId(group, service);
                        CachedServices[clusterId] = DateTime.Now;

                        var eventListener = Listener.GetOrAdd(clusterId, _ => new ServiceChangeEventListener(_logger!, this));
                        await _nameSvc.Subscribe(service, group, eventListener).ConfigureAwait(false);

                        // ClusterConfig
                        var instances = await _nameSvc.GetAllInstances(service, group, false).ConfigureAwait(false);
                        var cluster = _configMapper.CreateClusterConfig(clusterId, _configMapper.CreateDestinationConfig(instances));
                        clusters[clusterId] = cluster;
                        CachedClusters[clusterId] = cluster;

                        // RouteConfig
                        var route = _configMapper.CreateRouteConfig(clusterId, group, service);
                        routes[clusterId] = route;
                        CachedRoutes[clusterId] = route;
                    }
                    catch (Exception ex)
                    {
                        _logger?.Error($"create yarp cluster:{group}.{service} configuration failed", ex);
                    }
                }
            }

            return new YarpNacosProxyConfig(routes.Values.ToList(), clusters.Values.ToList());
        }

        /// <summary>
        /// 获取当前缓存中所有集群服务的标识列表
        /// </summary>
        public List<string> GetCachedClusterList() => CachedServices.Select(x => x.Key).ToList();

        /// <summary>
        /// Nacos 服务变更事件监听器。当 Nacos 推送服务实例变更时触发，
        /// 负责更新缓存的 Yarp 代理配置并触发配置重载
        /// </summary>
        internal sealed class ServiceChangeEventListener : IEventListener
        {
            private readonly IYarpNacosLogger _logger;
            private readonly YarpNacosStore _store;

            public ServiceChangeEventListener(IYarpNacosLogger logger, YarpNacosStore store)
            {
                _logger = logger;
                _store = store;
            }

            /// <summary>
            /// 处理 Nacos 服务变更事件，更新 Yarp 配置并触发重载
            /// </summary>
            public async Task OnEvent(IEvent @event)
            {
                var traceId = Guid.NewGuid().ToString("N");
                var e = (InstancesChangeEvent)@event;
                if (e == null)
                    return;

                try
                {
                    var clusterId = YarpNacosUtils.CreateClusterId(e.GroupName, e.ServiceName);

                    if (!_store.CachedClusters.ContainsKey(clusterId) && e.Hosts.Count == 0)
                        return;

                    if (_store.CachedClusters.ContainsKey(clusterId))
                    {
                        if (e.Hosts.Count > 0)
                        {
                            try
                            {
                                var instances = await _store._nameSvc.GetAllInstances(e.ServiceName, e.GroupName, false).ConfigureAwait(false);
                                var cluster = _store._configMapper.CreateClusterConfig(clusterId, _store._configMapper.CreateDestinationConfig(instances));
                                _store.CachedClusters[clusterId] = cluster;
                                _logger?.Info($"{traceId} -> nacos service listener[{$"{e.ServiceName}/{e.GroupName}"}] -> detected that service changes,update yarp proxy configuration");
                                _store.Reload();
                            }
                            catch (Exception ex)
                            {
                                _logger?.Error($"{traceId} -> nacos service listener[{$"{e.ServiceName}/{e.GroupName}"}] -> detected that service changes,update yarp configuration exception", ex);
                            }
                        }
                        else
                        {
                            _store.CachedClusters.Remove(clusterId, out _);
                            _store.CachedRoutes.Remove(clusterId, out _);
                            _logger?.Info($"{traceId} -> nacos service listener[{$"{e.ServiceName}/{e.GroupName}"}] -> clusters:[{$"{e.ServiceName}/{e.GroupName}"}] no healthy instance，remove yarp proxy configuration");
                        }
                    }
                    else
                    {
                        try
                        {
                            _store.CachedServices[clusterId] = DateTime.Now;
                            var instances = await _store._nameSvc.GetAllInstances(e.ServiceName, e.GroupName, false).ConfigureAwait(false);
                            var cluster = _store._configMapper.CreateClusterConfig(clusterId, _store._configMapper.CreateDestinationConfig(instances));
                            _store.CachedClusters[clusterId] = cluster;
                            var route = _store._configMapper.CreateRouteConfig(clusterId, e.GroupName, e.ServiceName);
                            _store.CachedRoutes[clusterId] = route;
                            _logger?.Info($"{traceId} -> nacos service listener[{$"{e.ServiceName}/{e.GroupName}"}] -> detected that new service,add yarp proxy configuration:{YarpNacosUtils.JsonSerialize(cluster)}");
                        }
                        catch (Exception ex)
                        {
                            _logger?.Error($"{traceId} -> nacos service listener[{$"{e.ServiceName}/{e.GroupName}"}] -> detected that new service,add yarp proxy configuration exception", ex);
                        }
                    }

                    _store.Reload();
                }
                catch (Exception ex)
                {
                    _logger.Error($"{traceId} -> nacos service listener[{$"{e.ServiceName}/{e.GroupName}"}] -> nacos service:[{$"{e.ServiceName}/{e.GroupName}"}] handle exception", ex);
                }
                finally
                {
                    _logger.Info($"{traceId} -> yarp proxy configuration：{YarpNacosUtils.JsonSerialize(_store.CachedClusters.Select(x => x.Value).ToList())}");
                }
            }
        }
    }
}
