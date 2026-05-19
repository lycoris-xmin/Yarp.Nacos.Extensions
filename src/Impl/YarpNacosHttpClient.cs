using Lycoris.Yarp.Nacos.Extensions.Logging;
using Lycoris.Yarp.Nacos.Extensions.Options;
using Microsoft.Extensions.Options;
using Nacos.V2;
using System.Text;
using System.Text.Json;

namespace Lycoris.Yarp.Nacos.Extensions.Impl
{
    /// <summary>
    /// Yarp Nacos HTTP 客户端默认实现。
    /// 从 Nacos 发现服务实例后使用 HttpClient 发起调用，内置随机负载均衡、超时和重试。
    /// </summary>
    internal sealed class YarpNacosHttpClient : IYarpNacosHttpClient
    {
        /// <summary>JSON 序列化选项，忽略属性名大小写</summary>
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        /// <summary>HTTP 客户端实例</summary>
        private readonly HttpClient _httpClient;
        /// <summary>默认 Nacos 命名服务</summary>
        private readonly INacosNamingService _nameSvc;
        /// <summary>额外的 Namespace 命名服务映射</summary>
        private readonly IReadOnlyDictionary<string, INacosNamingService>? _namespaceClients;
        /// <summary>扩展配置选项</summary>
        private readonly YarpNacosOptions _options;
        /// <summary>日志记录器</summary>
        private readonly IYarpNacosLogger _logger;

        /// <summary>
        /// 初始化 HTTP 客户端
        /// </summary>
        /// <param name="httpClient">HTTP 客户端</param>
        /// <param name="nameSvc">默认 Nacos 命名服务</param>
        /// <param name="options">扩展配置选项</param>
        /// <param name="loggerFactory">日志工厂</param>
        /// <param name="namespaceClients">额外的 Namespace 命名服务映射</param>
        public YarpNacosHttpClient(HttpClient httpClient,
                                    INacosNamingService nameSvc,
                                    IOptions<YarpNacosOptions> options,
                                    IYarpNacosLoggerFactory loggerFactory,
                                    IReadOnlyDictionary<string, INacosNamingService>? namespaceClients = null)
        {
            _httpClient = httpClient;
            _nameSvc = nameSvc;
            _options = options.Value;
            _logger = loggerFactory.CreateLogger<YarpNacosHttpClient>();
            _namespaceClients = namespaceClients;
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        /// <summary>
        /// 发送 GET 请求，通过 Action 委托配置请求参数
        /// </summary>
        /// <typeparam name="TResponse">响应类型</typeparam>
        /// <param name="configure">请求配置委托</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>反序列化后的响应对象，无健康实例时返回 default</returns>
        public Task<TResponse?> GetAsync<TResponse>(Action<NacosHttpRequest> configure, CancellationToken cancellationToken = default)
            => SendAsync<TResponse>(HttpMethod.Get, BuildRequest(configure), cancellationToken);

        /// <summary>
        /// 发送 POST 请求，通过 Action 委托配置请求参数
        /// </summary>
        /// <typeparam name="TResponse">响应类型</typeparam>
        /// <param name="configure">请求配置委托</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>反序列化后的响应对象，无健康实例时返回 default</returns>
        public Task<TResponse?> PostAsync<TResponse>(Action<NacosHttpRequest> configure, CancellationToken cancellationToken = default)
            => SendAsync<TResponse>(HttpMethod.Post, BuildRequest(configure), cancellationToken);

        /// <summary>
        /// 发送 PUT 请求，通过 Action 委托配置请求参数
        /// </summary>
        /// <typeparam name="TResponse">响应类型</typeparam>
        /// <param name="configure">请求配置委托</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>反序列化后的响应对象，无健康实例时返回 default</returns>
        public Task<TResponse?> PutAsync<TResponse>(Action<NacosHttpRequest> configure, CancellationToken cancellationToken = default)
            => SendAsync<TResponse>(HttpMethod.Put, BuildRequest(configure), cancellationToken);

        /// <summary>
        /// 发送 DELETE 请求，通过 Action 委托配置请求参数
        /// </summary>
        /// <typeparam name="TResponse">响应类型</typeparam>
        /// <param name="configure">请求配置委托</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>反序列化后的响应对象，无健康实例时返回 default</returns>
        public Task<TResponse?> DeleteAsync<TResponse>(Action<NacosHttpRequest> configure, CancellationToken cancellationToken = default)
            => SendAsync<TResponse>(HttpMethod.Delete, BuildRequest(configure), cancellationToken);

        /// <summary>
        /// 发送 GET 请求，直接传入请求配置对象
        /// </summary>
        /// <typeparam name="TResponse">响应类型</typeparam>
        /// <param name="request">请求配置对象</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>反序列化后的响应对象，无健康实例时返回 default</returns>
        public Task<TResponse?> GetAsync<TResponse>(NacosHttpRequest request, CancellationToken cancellationToken = default)
            => SendAsync<TResponse>(HttpMethod.Get, request, cancellationToken);

        /// <summary>
        /// 发送 POST 请求，直接传入请求配置对象
        /// </summary>
        /// <typeparam name="TResponse">响应类型</typeparam>
        /// <param name="request">请求配置对象</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>反序列化后的响应对象，无健康实例时返回 default</returns>
        public Task<TResponse?> PostAsync<TResponse>(NacosHttpRequest request, CancellationToken cancellationToken = default)
            => SendAsync<TResponse>(HttpMethod.Post, request, cancellationToken);

        /// <summary>
        /// 发送 PUT 请求，直接传入请求配置对象
        /// </summary>
        /// <typeparam name="TResponse">响应类型</typeparam>
        /// <param name="request">请求配置对象</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>反序列化后的响应对象，无健康实例时返回 default</returns>
        public Task<TResponse?> PutAsync<TResponse>(NacosHttpRequest request, CancellationToken cancellationToken = default)
            => SendAsync<TResponse>(HttpMethod.Put, request, cancellationToken);

        /// <summary>
        /// 发送 DELETE 请求，直接传入请求配置对象
        /// </summary>
        /// <typeparam name="TResponse">响应类型</typeparam>
        /// <param name="request">请求配置对象</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>反序列化后的响应对象，无健康实例时返回 default</returns>
        public Task<TResponse?> DeleteAsync<TResponse>(NacosHttpRequest request, CancellationToken cancellationToken = default)
            => SendAsync<TResponse>(HttpMethod.Delete, request, cancellationToken);

        /// <summary>
        /// 通过 Action 委托构建请求配置对象
        /// </summary>
        /// <param name="configure">请求配置委托</param>
        /// <returns>构建完成的请求配置对象</returns>
        private static NacosHttpRequest BuildRequest(Action<NacosHttpRequest> configure)
        {
            var request = new NacosHttpRequest();
            configure(request);
            return request;
        }

        /// <summary>
        /// 核心发送逻辑：Nacos 服务发现 → 随机选取健康实例 → HTTP 请求 → 反序列化响应。
        /// 支持超时控制和失败重试。
        /// </summary>
        /// <typeparam name="TResponse">响应类型</typeparam>
        /// <param name="method">HTTP 方法</param>
        /// <param name="req">请求配置</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>反序列化后的响应对象，失败时返回 default</returns>
        private async Task<TResponse?> SendAsync<TResponse>(HttpMethod method, NacosHttpRequest req, CancellationToken cancellationToken)
        {
            var nameSvc = GetNamingServiceForGroup(req.GroupName);
            var instances = await nameSvc.GetAllInstances(req.ServiceName, req.GroupName, false).ConfigureAwait(false);

            var healthy = instances.Where(x => x.Healthy && x.Enabled).ToList();
            if (healthy.Count == 0)
            {
                _logger.Warn($"no healthy instance found for service: {req.GroupName}/{req.ServiceName}");
                return default;
            }

            // 随机选取一个健康实例
            var instance = healthy[Random.Shared.Next(healthy.Count)];
            var scheme = instance.Metadata.TryGetValue("secure", out _) ? "https" : "http";
            var url = $"{scheme}://{instance.Ip}:{instance.Port}{req.Path}";

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(req.TimeoutSeconds));

            var httpRequest = new HttpRequestMessage(method, url);
            if (req.Body != null)
            {
                var json = JsonSerializer.Serialize(req.Body, JsonOptions);
                httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            foreach (var header in req.Headers)
                httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);

            for (int attempt = 0; attempt <= req.RetryCount; attempt++)
            {
                try
                {
                    var response = await _httpClient.SendAsync(httpRequest, cts.Token).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();

                    var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    return JsonSerializer.Deserialize<TResponse>(content, JsonOptions);
                }
                catch (Exception ex) when (attempt < req.RetryCount && !cts.IsCancellationRequested)
                {
                    _logger.Warn($"request to {req.ServiceName}{req.Path} failed (attempt {attempt + 1}/{req.RetryCount + 1}), retrying: {ex.Message}");
                }
            }

            return default;
        }

        /// <summary>
        /// 根据群组名获取对应的 Nacos 命名服务。
        /// 优先查找 GroupNamespaceMap 中配置的自定义 Namespace 客户端，未配置则使用默认实例。
        /// </summary>
        /// <param name="groupName">群组名称</param>
        /// <returns>对应的 INacosNamingService 实例</returns>
        private INacosNamingService GetNamingServiceForGroup(string groupName)
        {
            if (_namespaceClients != null && _options.GroupNamespaceMap.TryGetValue(groupName, out var ns) && _namespaceClients.TryGetValue(ns, out var client))
                return client;

            return _nameSvc;
        }
    }
}
