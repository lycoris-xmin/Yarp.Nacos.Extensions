namespace Lycoris.Yarp.Nacos.Extensions
{
    /// <summary>
    /// Nacos HTTP 请求配置，用于 <see cref="IYarpNacosHttpClient"/> 的方法调用。
    /// 包含服务名称、请求路径、群组、超时、重试、请求头等完整配置。
    /// </summary>
    public class NacosHttpRequest
    {
        /// <summary>
        /// Nacos 服务名称（必填）
        /// </summary>
        public string ServiceName { get; set; } = string.Empty;

        /// <summary>
        /// 请求路径，如 /api/users/1（必填）
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Nacos 群组名称，默认 DEFAULT_GROUP
        /// </summary>
        public string GroupName { get; set; } = "DEFAULT_GROUP";

        /// <summary>
        /// 请求体，发送 JSON 时自动序列化。GET/DELETE 请求忽略此属性。
        /// </summary>
        public object? Body { get; set; }

        /// <summary>
        /// 单次请求超时时间（秒），默认 30 秒
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// 失败重试次数，默认 0 表示不重试
        /// </summary>
        public int RetryCount { get; set; } = 0;

        /// <summary>
        /// 自定义请求头，Key 为头名称，Value 为头值
        /// </summary>
        public Dictionary<string, string> Headers { get; set; } = new();
    }
}
