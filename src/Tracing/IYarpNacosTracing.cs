namespace Lycoris.Yarp.Nacos.Extensions.Tracing
{
    /// <summary>
    /// 链路追踪抽象接口。
    /// 用户可实现此接口接入 OpenTelemetry、SkyWalking 或自定义追踪系统。
    /// 通过 <see cref="YarpNacosPaoxyBuilder.AddTracing{T}"/> 注册，未注册时使用无操作默认实现。
    /// </summary>
    public interface IYarpNacosTracing
    {
        /// <summary>
        /// 为出站 HTTP 请求注入追踪上下文头，实现跨服务链路传播。
        /// 例如 W3C Trace Context（traceparent / tracestate）、SkyWalking（sw8）等。
        /// </summary>
        /// <param name="request">待发送的 HTTP 请求消息</param>
        void EnrichRequest(HttpRequestMessage request);

        /// <summary>
        /// 开始一个追踪 Span，用于标记关键操作的时间范围。
        /// using 结束时自动完成 Span。不需要追踪时返回 null。
        /// </summary>
        /// <param name="operationName">操作名称，如 "nacos.heartbeat"、"nacos.get_services"</param>
        /// <param name="kind">Span 类型</param>
        /// <param name="tags">附加标签键值对，可为 null</param>
        /// <returns>Span 的 IDisposable 句柄，using 释放时自动结束 Span；不需要追踪时返回 null</returns>
        IDisposable? BeginSpan(string operationName, SpanKind kind = SpanKind.Internal, Dictionary<string, string>? tags = null);
    }
}
