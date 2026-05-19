namespace Lycoris.Yarp.Nacos.Extensions
{
    /// <summary>
    /// Yarp Nacos 扩展常量定义
    /// </summary>
    public class YarpNacosConstants
    {
        /// <summary>
        /// Nacos 实例权重元数据 Key，对应 Nacos 实例的 Weight 属性
        /// </summary>
        public const string InstanceWeight = "Weight";

        /// <summary>
        /// 基于权重的负载均衡策略名称
        /// </summary>
        internal const string WeightLoadBalancingPolicy = "WeightLoadBalancingPolicy";
    }
}
