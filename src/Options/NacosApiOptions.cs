namespace Lycoris.Yarp.Nacos.Extensions.Options
{
    /// <summary>
    /// 单个聚合 API 服务的配置选项。
    /// 用于 <see cref="YarpNacosPaoxyBuilder.AddApi{TInterface, TImpl}(Action{NacosApiOptions}?)"/> 注册时，
    /// 配置 API 的暴露路径、默认服务名、超时重试等参数。
    /// </summary>
    public class NacosApiOptions
    {
        /// <summary>
        /// API 在网关上暴露的基础路径，如 /api/orders。
        /// 用于路由匹配和文档说明。
        /// </summary>
        public string? BasePath { get; set; }

        /// <summary>
        /// 默认调用的 Nacos 服务名称。
        /// API 实现中可通过 <see cref="IYarpNacosHttpClient"/> 调用其他服务。
        /// </summary>
        public string? ServiceName { get; set; }

        /// <summary>
        /// 默认调用的 Nacos 群组名称，默认 "DEFAULT_GROUP"
        /// </summary>
        public string GroupName { get; set; } = "DEFAULT_GROUP";

        /// <summary>
        /// HTTP 请求超时时间（秒），默认 30 秒
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// 失败重试次数，默认 0 表示不重试
        /// </summary>
        public int RetryCount { get; set; } = 0;
    }
}
