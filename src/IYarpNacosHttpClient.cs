namespace Lycoris.Yarp.Nacos.Extensions
{
    /// <summary>
    /// Yarp Nacos HTTP 客户端接口。
    /// 封装了基于 Nacos 服务发现的 HTTP 调用，用于在聚合 API 实现中调用 Nacos 注册的微服务。
    /// 支持两种调用方式：直接传入 <see cref="NacosHttpRequest"/> 或通过 <see cref="Action{NacosHttpRequest}"/> 委托配置。
    /// 内部自动完成服务发现、负载均衡（随机选取健康实例）、超时控制、失败重试。
    /// </summary>
    public interface IYarpNacosHttpClient
    {
        /// <summary>
        /// 发送 GET 请求（Action 委托配置模式）
        /// </summary>
        /// <typeparam name="TResponse">响应反序列化类型</typeparam>
        /// <param name="configure">请求配置委托</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>反序列化后的响应对象，无健康实例时返回 default</returns>
        Task<TResponse?> GetAsync<TResponse>(Action<NacosHttpRequest> configure, CancellationToken cancellationToken = default);

        /// <summary>
        /// 发送 GET 请求（对象传入模式）
        /// </summary>
        /// <typeparam name="TResponse">响应反序列化类型</typeparam>
        /// <param name="request">请求配置对象</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>反序列化后的响应对象，无健康实例时返回 default</returns>
        Task<TResponse?> GetAsync<TResponse>(NacosHttpRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// 发送 POST 请求（Action 委托配置模式）
        /// </summary>
        /// <typeparam name="TResponse">响应反序列化类型</typeparam>
        /// <param name="configure">请求配置委托</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>反序列化后的响应对象，无健康实例时返回 default</returns>
        Task<TResponse?> PostAsync<TResponse>(Action<NacosHttpRequest> configure, CancellationToken cancellationToken = default);

        /// <summary>
        /// 发送 POST 请求（对象传入模式）
        /// </summary>
        /// <typeparam name="TResponse">响应反序列化类型</typeparam>
        /// <param name="request">请求配置对象</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>反序列化后的响应对象，无健康实例时返回 default</returns>
        Task<TResponse?> PostAsync<TResponse>(NacosHttpRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// 发送 PUT 请求（Action 委托配置模式）
        /// </summary>
        /// <typeparam name="TResponse">响应反序列化类型</typeparam>
        /// <param name="configure">请求配置委托</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>反序列化后的响应对象，无健康实例时返回 default</returns>
        Task<TResponse?> PutAsync<TResponse>(Action<NacosHttpRequest> configure, CancellationToken cancellationToken = default);

        /// <summary>
        /// 发送 PUT 请求（对象传入模式）
        /// </summary>
        /// <typeparam name="TResponse">响应反序列化类型</typeparam>
        /// <param name="request">请求配置对象</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>反序列化后的响应对象，无健康实例时返回 default</returns>
        Task<TResponse?> PutAsync<TResponse>(NacosHttpRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// 发送 DELETE 请求（Action 委托配置模式）
        /// </summary>
        /// <typeparam name="TResponse">响应反序列化类型</typeparam>
        /// <param name="configure">请求配置委托</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>反序列化后的响应对象，无健康实例时返回 default</returns>
        Task<TResponse?> DeleteAsync<TResponse>(Action<NacosHttpRequest> configure, CancellationToken cancellationToken = default);

        /// <summary>
        /// 发送 DELETE 请求（对象传入模式）
        /// </summary>
        /// <typeparam name="TResponse">响应反序列化类型</typeparam>
        /// <param name="request">请求配置对象</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>反序列化后的响应对象，无健康实例时返回 default</returns>
        Task<TResponse?> DeleteAsync<TResponse>(NacosHttpRequest request, CancellationToken cancellationToken = default);
    }
}
