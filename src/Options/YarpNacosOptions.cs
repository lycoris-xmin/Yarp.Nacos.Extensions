namespace Lycoris.Yarp.Nacos.Extensions.Options
{
    /// <summary>
    /// Yarp Nacos 扩展配置选项
    /// </summary>
    public class YarpNacosOptions
    {
        /// <summary>
        /// 需要监听的 Nacos 服务群组名称列表
        /// </summary>
        public List<string> GroupNameList { get; set; } = new List<string>();

        /// <summary>
        /// 从 Nacos 拉取服务列表时每页的预取数量。
        /// Nacos 2.0.3 版本的 gRPC 可能存在计数异常，建议设置较大的值以避免翻页问题。
        /// </summary>
        public int PreCount { get; set; } = 50;

        /// <summary>
        /// 集群服务上下线心跳监听间隔（单位：秒），默认 5 秒
        /// </summary>
        public int NacosServicesHeartbeat { get; set; } = 5;

        /// <summary>
        /// 负载均衡策略名称，为空时使用 Yarp 默认策略。
        /// 可通过 <see cref="YarpNacosPaoxyBuilder.AddWeightLoadBalancingPolicy"/> 启用权重负载均衡，
        /// 或通过 <see cref="YarpNacosPaoxyBuilder.AddLoadBalancingPolicy{T}(string)"/> 使用自定义策略。
        /// </summary>
        public string? LoadBalancingPolicyName { get; set; }
    }
}
