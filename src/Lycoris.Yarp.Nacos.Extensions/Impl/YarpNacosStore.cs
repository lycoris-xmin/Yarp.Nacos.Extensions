using Lycoris.Base.Extensions;
using Lycoris.Base.Logging;
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
    /// 
    /// </summary>
    public sealed class YarpNacosStore : IYarpNacosStore
    {
        private YarpNacosReloadToken _reloadToken = new();
        private readonly ILycorisLogger _logger;
        private readonly YarpNacosOptions _options;
        private readonly INacosNamingService _nameSvc;
        private readonly IYarpNacosPaoxyConfigMapper _configMapper;

        private readonly ConcurrentDictionary<string, DateTime> CachedServices = new();
        private readonly ConcurrentDictionary<string, RouteConfig> CachedRoutes = new();
        private readonly ConcurrentDictionary<string, ClusterConfig> CachedClusters = new();

        private readonly Dictionary<string, ServiceChangeEventListener> Listener = new();

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="factory"></param>
        /// <param name="optionsAccs"></param>
        /// <param name="nameSvc"></param>
        /// <param name="configMapper"></param>
        public YarpNacosStore(ILycorisLoggerFactory factory,
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
        /// 获取重载令牌
        /// get reloadToken
        /// </summary>
        /// <returns></returns>
        public IChangeToken GetReloadToken() => _reloadToken;

        /// <summary>
        /// 重新载入配置
        /// reload configuration
        /// </summary>
        public void Reload() => Interlocked.Exchange(ref _reloadToken, new YarpNacosReloadToken()).OnReload();

        /// <summary>
        /// 获取yarp 反向代理配置规则
        /// get yarp reverse proxy configuration rules
        /// </summary>
        /// <returns></returns>
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
        /// 获取Nacos上指定群组的所有服务
        /// get all services of the specified group on nacos
        /// </summary>
        /// <returns></returns>
        public async Task<Dictionary<string, List<string>>> GetNacosGroupServicesAsync()
        {
            var groupServicesDict = new Dictionary<string, List<string>>();

            foreach (var groupName in _options.GroupNameList)
            {
                try
                {
                    int pageIndex = 1;

                    var listView = await _nameSvc.GetServicesOfServer(pageIndex, _options.PreCount, groupName).ConfigureAwait(false); ;

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
                            groupServices.AddRange(tmp.Data);
                        }
                        while (listView.Count > _options.PreCount * pageIndex);
                    }

                    // 其他服务添加至群组中
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
        /// 添加集群服务监听
        /// </summary>
        /// <param name="clusterServices"></param>
        /// <returns></returns>
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
                        // 只需要添加监听即可，因为首次添加监听后，Nacos会马上推送服务变更信息，配置的新增及重载交给监听事件处理
                        // 监听服务Nacos有时候不能正常推送，所以需要做一个延迟确认配置是否正常更新的处理
                        var clusterId = YarpNacosUtils.CreateClusterId(groupName, service);
                        if (!Listener.ContainsKey(clusterId))
                            Listener.Add(clusterId, new ServiceChangeEventListener(_logger!, this));
                        await _nameSvc.Subscribe(service, groupName, Listener[clusterId]).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        // 处理失败的添加至失败列表
                        _logger?.Error($"subscribe nacos service linterer：{groupName}.{service} failed", ex);
                    }
                }
            }
        }

        /// <summary>
        /// 移除集群服务反向代理配置
        /// </summary>
        /// <param name="groupServices"></param>
        /// <returns></returns>
        public async Task RemoveClusterProxyConfigAsync(List<string>? groupServices)
        {
            if (groupServices == null || !groupServices.Any())
                return;

            foreach (var item in groupServices)
            {
                CachedServices.Remove(item, out _);
                CachedClusters.Remove(item, out _);
                CachedRoutes.Remove(item, out _);

                // 获取到对应的组别和微服务名称
                var (group, service) = YarpNacosUtils.GetGroupService(item);
                // 移除事件监听
                if (Listener.ContainsKey(item))
                    await _nameSvc.Unsubscribe(service, group, Listener[item]).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 添加集群服务yarp反向代理规则
        /// </summary>
        /// <param name="groupServices"></param>
        /// <returns></returns>
        public async Task AddClusterProxyConfigAsync(Dictionary<string, List<string>>? groupServices)
        {
            if (groupServices == null || !groupServices.Any())
                return;

            foreach (var item in groupServices)
            {
                var group = item.Key;
                foreach (var service in item.Value)
                {
                    // group + service = uniqueId
                    var clusterId = YarpNacosUtils.CreateClusterId(group, service);
                    CachedServices[clusterId] = DateTime.Now;

                    try
                    {
                        // 移除原有的事件监听
                        if (Listener.ContainsKey(clusterId))
                            await _nameSvc.Unsubscribe(service, group, Listener[clusterId]).ConfigureAwait(false);
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
                        if (!Listener.ContainsKey(clusterId))
                            Listener.Add(clusterId, new ServiceChangeEventListener(_logger!, this));

                        await _nameSvc.Subscribe(service, group, Listener[clusterId]).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger?.Error($"add yarp cluster:{group}.{service} configuration failed", ex);
                    }

                }
            }
        }

        /// <summary>
        /// 创建yarp反向代理配置规则
        /// create yarp reverse proxy configuration rules
        /// </summary>
        /// <param name="groupServices"></param>
        /// <returns></returns>
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
                        // group + service = uniqueId
                        var clusterId = YarpNacosUtils.CreateClusterId(group, service);
                        CachedServices[clusterId] = DateTime.Now;

                        if (!Listener.ContainsKey(clusterId))
                            Listener.Add(clusterId, new ServiceChangeEventListener(_logger!, this));
                        await _nameSvc.Subscribe(service, group, Listener[clusterId]).ConfigureAwait(false);

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
        /// 
        /// </summary>
        /// <returns></returns>
        public List<string> GetCachedClusterList() => CachedServices.Select(x => x.Key).ToList();

        /// <summary>
        /// Nacos服务监听
        /// Nacos service monitoring
        /// </summary>
        internal sealed class ServiceChangeEventListener : IEventListener
        {
            private readonly ILycorisLogger _logger;
            private readonly YarpNacosStore _store;

            public ServiceChangeEventListener(ILycorisLogger logger, YarpNacosStore store)
            {
                _logger = logger;
                _store = store;
            }

            public async Task OnEvent(IEvent @event)
            {
                var traceId = Guid.NewGuid().ToString("N");
                var e = (InstancesChangeEvent)@event;
                if (e == null)
                    return;

                try
                {
                    // 创建唯一Id
                    var clusterId = YarpNacosUtils.CreateClusterId(e.GroupName, e.ServiceName);

                    if (!_store.CachedClusters.ContainsKey(clusterId) && e.Hosts.Count == 0)
                        return;

                    if (_store.CachedClusters.ContainsKey(clusterId))
                    {
                        if (e.Hosts.Count > 0)
                        {
                            try
                            {
                                // 从 nacos 服务器中查找最新实例。
                                var instances = await _store._nameSvc.GetAllInstances(e.ServiceName, e.GroupName, false).ConfigureAwait(false);

                                // 重新创建配置
                                var cluster = _store._configMapper.CreateClusterConfig(clusterId, _store._configMapper.CreateDestinationConfig(instances));

                                // 更新配置
                                _store.CachedClusters[clusterId] = cluster;

                                // 日志记录
                                _logger?.Info($"{traceId} -> nacos service listener[{$"{e.ServiceName}/{e.GroupName}"}] -> detected that service changes,update yarp proxy configuration");

                                // 配置重载
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

                            // ClusterConfig
                            var instances = await _store._nameSvc.GetAllInstances(e.ServiceName, e.GroupName, false).ConfigureAwait(false);
                            var cluster = _store._configMapper.CreateClusterConfig(clusterId, _store._configMapper.CreateDestinationConfig(instances));
                            _store.CachedClusters[clusterId] = cluster;

                            // RouteConfig
                            var route = _store._configMapper.CreateRouteConfig(clusterId, e.GroupName, e.ServiceName);
                            _store.CachedRoutes[clusterId] = route;

                            // 日志记录
                            _logger?.Info($"{traceId} -> nacos service listener[{$"{e.ServiceName}/{e.GroupName}"}] -> detected that new service,add yarp proxy configuration:{YarpNacosUtils.JsonSerialize(cluster)}");
                        }
                        catch (Exception ex)
                        {
                            _logger?.Error($"{traceId} -> nacos service listener[{$"{e.ServiceName}/{e.GroupName}"}] -> detected that new service,add yarp proxy configuration exception", ex);
                        }
                    }

                    // 配置重载
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