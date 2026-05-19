namespace Lycoris.Yarp.Nacos.Extensions.Tracing
{
    /// <summary>
    /// 无操作链路追踪默认实现。未注册自定义追踪时使用，所有方法均为空操作。
    /// </summary>
    internal sealed class NoopYarpNacosTracing : IYarpNacosTracing
    {
        /// <summary>
        /// 单例实例
        /// </summary>
        public static readonly NoopYarpNacosTracing Instance = new();

        /// <summary>
        /// 空操作，不注入任何追踪头
        /// </summary>
        public void EnrichRequest(HttpRequestMessage request) { }

        /// <summary>
        /// 空操作，始终返回 null 表示不创建 Span
        /// </summary>
        public IDisposable? BeginSpan(string operationName, SpanKind kind = SpanKind.Internal, Dictionary<string, string>? tags = null) => null;
    }
}
