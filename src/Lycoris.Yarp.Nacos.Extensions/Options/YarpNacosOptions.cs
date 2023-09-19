namespace Lycoris.Yarp.Nacos.Extensions.Options
{
    /// <summary>
    /// 
    /// </summary>
    public class YarpNacosOptions
    {
        /// <summary>
        /// 微服务群组列表
        /// </summary>
        public List<string> GroupNameList { get; set; } = new List<string>();

        /// <summary>
        /// 获取微服务下的实例列表
        /// Nacos 2.0.3 版本的GRPC有些Bug，不会返回正常的计数
        /// 为避免此问题，请将预计数设置为更大的值
        /// </summary>
        public int PreCount { get; set; } = 50;

        /// <summary>
        /// 集群服务上下线监听时间(单位：秒)
        /// </summary>
        public int NacosServicesHeartbeat { get; set; } = 5;

        /// <summary>
        /// 负载均衡策略
        /// </summary>
        public string? LoadBalancingPolicyName { get; set; }
    }
}