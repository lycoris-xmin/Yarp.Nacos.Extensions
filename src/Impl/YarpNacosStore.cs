using Lycoris.Yarp.Nacos.Extensions.Logging;
using Lycoris.Yarp.Nacos.Extensions.Options;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Nacos.V2;
using Nacos.V2.Naming.Dtos;
using Nacos.V2.Naming.Event;
using System.Collections.Concurrent;
using Yarp.ReverseProxy.Configuration;

namespace Lycoris.Yarp.Nacos.Extensions.Impl
{
    /// <summary>
    /// Nacos 状态管理器。负责与 Nacos 服务发现交互，管理 Yarp 代理配置的缓存、
    /// 服务订阅/取消订阅以及配置的热重载。
    /// </summary>
    public sealed class YarpNacosStore : IYarpNacosStore, IDisposable
    {
        private YarpNacosReloadToken _reloadToken = new();
        private readonly IYarpNacosLogger _logger;
        private readonly YarpNacosOptions _options;
        private readonly INacosNamingService _defaultNameSvc;
        private readonly IReadOnlyDictionary<string, INacosNamingService>? _namespaceClients;
        private readonly IYarpNacosPaoxyConfigMapper _configMapper;
        private readonly IReadOnlyList<IYarpNacosServiceChangeListener> _serviceChangeListeners;
        private bool _disposed;

        private readonly ConcurrentDictionary<string, DateTime> CachedServices = new();
        private readonly ConcurrentDictionary<string, RouteConfig> CachedRoutes = new();
        private readonly ConcurrentDictionary<string, ClusterConfig> CachedClusters = new();

        private readonly ConcurrentDictionary<string, ServiceChangeEventListener> Listener = new();

        /// <summary>
        /// 初始化 Nacos 状态管理器
        /// </summary>
        /// <param name="factory">日志工厂</param>
        /// <param name="optionsAccs">扩展配置选项</param>
        /// <param name="nameSvc">默认 Nacos 命名服务</param>
        /// <param name="configMapper">配置映射器</param>
        /// <param name="namespaceClients">可选的各组 Namespace 命名服务映射</param>
        /// <param name="serviceChangeListeners">服务变更监听器集合</param>
        public YarpNacosStore(IYarpNacosLoggerFactory factory,
                              IOptions<YarpNacosOptions> optionsAccs,
                              INacosNamingService nameSvc,
                              IYarpNacosPaoxyConfigMapper configMapper,
                              IReadOnlyDictionary<string, INacosNamingService>? namespaceClients = null,
                              IEnumerable<IYarpNacosServiceChangeListener>? serviceChangeListeners = null)
        {
            _logger = factory.CreateLogger<YarpNacosStore>();
            _options = optionsAccs.Value;
            _defaultNameSvc = nameSvc;
            _configMapper = configMapper;
            _namespaceClients = namespaceClients;
            _serviceChangeListeners = serviceChangeListeners?.ToList() ?? new List<IYarpNacosServiceChangeListener>();
        }

        /// <summary>
        /// 释放资源，取消所有 Nacos 服务监听
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            foreach (var kvp in Listener)
            {
                try
                {
                    var (group, service) = YarpNacosUtils.GetGroupService(kvp.Key);
                    var nameSvc = GetNamingServiceForGroup(group);
                    nameSvc.Unsubscribe(service, group, kvp.Value).ConfigureAwait(false).GetAwaiter().GetResult();
                }
                catch
                {
                    // 关闭时忽略取消订阅的异常
                }
            }

            Listener.Clear();
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
        /// 获取 Nacos 上配置的所有群组的微服务列表，支持翻页和多 Namespace
        /// </summary>
        public async Task<Dictionary<string, List<string>>> GetNacosGroupServicesAsync()
        {
            var groupServicesDict = new Dictionary<string, List<string>>();

            foreach (var groupName in _options.GroupNameList)
            {
                try
                {
                    var nameSvc = GetNamingServiceForGroup(groupName);
                    int pageIndex = 1;

                    var listView = await nameSvc.GetServicesOfServer(pageIndex, _options.PreCount, groupName).ConfigureAwait(false);

                    if (listView.Count == 0)
                    {
                        groupServicesDict.Add(groupName, new List<string>());
                        continue;
                    }

                    var groupServices = listView.Data;

                    if (listView.Count > _options.PreCount)
                    {
                        do
                        {
                            pageIndex++;
                            var tmp = await nameSvc.GetServicesOfServer(pageIndex, _options.PreCount, groupName).ConfigureAwait(false);
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
                var nameSvc = GetNamingServiceForGroup(groupName);

                foreach (var service in cluster.Value)
                {
                    try
                    {
                        var clusterId = YarpNacosUtils.CreateClusterId(groupName, service);
                        var eventListener = Listener.GetOrAdd(clusterId, _ => new ServiceChangeEventListener(_logger!, this));
                        await nameSvc.Subscribe(service, groupName, eventListener).ConfigureAwait(false);
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
                var nameSvc = GetNamingServiceForGroup(group);
                if (Listener.TryRemove(item, out var eventListener))
                    await nameSvc.Unsubscribe(service, group, eventListener).ConfigureAwait(false);
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
                var nameSvc = GetNamingServiceForGroup(group);

                foreach (var service in item.Value)
                {
                    var clusterId = YarpNacosUtils.CreateClusterId(group, service);
                    CachedServices[clusterId] = DateTime.Now;

                    try
                    {
                        if (Listener.TryRemove(clusterId, out var oldListener))
                            await nameSvc.Unsubscribe(service, group, oldListener).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger?.Error($"remove the cluster:{group}.{service} linterer failed", ex);
                        continue;
                    }

                    try
                    {
                        var instances = await nameSvc.GetAllInstances(service, group, false).ConfigureAwait(false);
                        instances = FilterInstancesByMetadata(instances);
                        var cluster = _configMapper.CreateClusterConfig(clusterId, _configMapper.CreateDestinationConfig(instances));
                        CachedClusters[clusterId] = cluster;

                        var route = _configMapper.CreateRouteConfig(clusterId, group, service);
                        CachedRoutes[clusterId] = route;

                        var eventListener = Listener.GetOrAdd(clusterId, _ => new ServiceChangeEventListener(_logger!, this));
                        await nameSvc.Subscribe(service, group, eventListener).ConfigureAwait(false);
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
                var nameSvc = GetNamingServiceForGroup(group);

                foreach (var service in item.Value)
                {
                    try
                    {
                        var clusterId = YarpNacosUtils.CreateClusterId(group, service);
                        CachedServices[clusterId] = DateTime.Now;

                        var eventListener = Listener.GetOrAdd(clusterId, _ => new ServiceChangeEventListener(_logger!, this));
                        await nameSvc.Subscribe(service, group, eventListener).ConfigureAwait(false);

                        var instances = await nameSvc.GetAllInstances(service, group, false).ConfigureAwait(false);
                        instances = FilterInstancesByMetadata(instances);
                        var cluster = _configMapper.CreateClusterConfig(clusterId, _configMapper.CreateDestinationConfig(instances));
                        clusters[clusterId] = cluster;
                        CachedClusters[clusterId] = cluster;

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
        /// 根据群组名获取对应的 Nacos 命名服务。
        /// 优先使用 GroupNamespaceMap 中指定的 Namespace 对应客户端，未配置则使用默认。
        /// </summary>
        private INacosNamingService GetNamingServiceForGroup(string groupName)
        {
            if (_namespaceClients != null && _options.GroupNamespaceMap.TryGetValue(groupName, out var ns) && _namespaceClients.TryGetValue(ns, out var client))
                return client;

            return _defaultNameSvc;
        }

        /// <summary>
        /// 按 InstanceMetadataFilter 配置过滤实例，仅保留元数据匹配的实例。
        /// 未配置过滤条件时直接返回原列表。
        /// </summary>
        private List<Instance> FilterInstancesByMetadata(List<Instance> instances)
        {
            if (_options.InstanceMetadataFilter == null || _options.InstanceMetadataFilter.Count == 0)
                return instances;

            return instances.Where(instance =>
            {
                if (instance.Metadata == null)
                    return false;

                foreach (var filter in _options.InstanceMetadataFilter)
                {
                    if (!instance.Metadata.TryGetValue(filter.Key, out var value) || !string.Equals(value, filter.Value, StringComparison.OrdinalIgnoreCase))
                        return false;
                }
                return true;
            }).ToList();
        }

        /// <summary>
        /// 通知所有已注册的服务变更监听器。
        /// 采用 fire-and-forget 模式，单个监听器的异常不会影响其他监听器。
        /// </summary>
        /// <param name="groupName">Nacos 群组名称</param>
        /// <param name="serviceName">Nacos 服务名称</param>
        /// <param name="instances">当前健康实例列表</param>
        /// <param name="isOnline">是否为上线事件</param>
        /// <param name="isChange">是否为实例变更事件</param>
        /// <param name="cancellationToken">取消令牌</param>
        private async Task NotifyListenersAsync(string groupName, string serviceName, List<Instance> instances, bool isOnline, bool isChange, CancellationToken cancellationToken)
        {
            if (_serviceChangeListeners.Count == 0)
                return;

            foreach (var listener in _serviceChangeListeners)
            {
                try
                {
                    if (isOnline)
                        await listener.OnServiceOnlineAsync(groupName, serviceName, instances, cancellationToken).ConfigureAwait(false);
                    else if (isChange)
                        await listener.OnServiceChangedAsync(groupName, serviceName, instances, cancellationToken).ConfigureAwait(false);
                    else
                        await listener.OnServiceOfflineAsync(groupName, serviceName, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger?.Error($"service change listener {listener.GetType().Name} error for {groupName}/{serviceName}", ex);
                }
            }
        }

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
                    var nameSvc = _store.GetNamingServiceForGroup(e.GroupName);

                    if (!_store.CachedClusters.ContainsKey(clusterId) && e.Hosts.Count == 0)
                        return;

                    if (_store.CachedClusters.ContainsKey(clusterId))
                    {
                        if (e.Hosts.Count > 0)
                        {
                            try
                            {
                                var instances = await nameSvc.GetAllInstances(e.ServiceName, e.GroupName, false).ConfigureAwait(false);
                                instances = _store.FilterInstancesByMetadata(instances);
                                var cluster = _store._configMapper.CreateClusterConfig(clusterId, _store._configMapper.CreateDestinationConfig(instances));
                                _store.CachedClusters[clusterId] = cluster;
                                _logger?.Info($"{traceId} -> nacos service listener[{$"{e.ServiceName}/{e.GroupName}"}] -> detected that service changes,update yarp proxy configuration");
                                _store.Reload();

                                // 触发服务实例变更回调
                                _ = _store.NotifyListenersAsync(e.GroupName, e.ServiceName, instances, isOnline: false, isChange: true, CancellationToken.None);
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

                            // 触发服务下线回调
                            _ = _store.NotifyListenersAsync(e.GroupName, e.ServiceName, new List<Instance>(), isOnline: false, isChange: false, CancellationToken.None);
                        }
                    }
                    else
                    {
                        try
                        {
                            _store.CachedServices[clusterId] = DateTime.Now;
                            var instances = await nameSvc.GetAllInstances(e.ServiceName, e.GroupName, false).ConfigureAwait(false);
                            instances = _store.FilterInstancesByMetadata(instances);
                            var cluster = _store._configMapper.CreateClusterConfig(clusterId, _store._configMapper.CreateDestinationConfig(instances));
                            _store.CachedClusters[clusterId] = cluster;
                            var route = _store._configMapper.CreateRouteConfig(clusterId, e.GroupName, e.ServiceName);
                            _store.CachedRoutes[clusterId] = route;
                            _logger?.Info($"{traceId} -> nacos service listener[{$"{e.ServiceName}/{e.GroupName}"}] -> detected that new service,add yarp proxy configuration:{YarpNacosUtils.JsonSerialize(cluster)}");

                            // 触发服务上线回调
                            _ = _store.NotifyListenersAsync(e.GroupName, e.ServiceName, instances, isOnline: true, isChange: false, CancellationToken.None);
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
