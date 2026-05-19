namespace Lycoris.Yarp.Nacos.Extensions.Tracing
{
    /// <summary>
    /// 追踪 Span 类型，对应 OpenTelemetry SpanKind
    /// </summary>
    public enum SpanKind
    {
        /// <summary>内部操作</summary>
        Internal = 0,
        /// <summary>出站客户端调用</summary>
        Client = 1,
        /// <summary>入站服务端接收</summary>
        Server = 2,
        /// <summary>生产者发送消息</summary>
        Producer = 3,
        /// <summary>消费者接收消息</summary>
        Consumer = 4
    }
}
