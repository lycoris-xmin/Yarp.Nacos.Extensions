using Lycoris.Yarp.Nacos.Extensions.Impl;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.LoadBalancing;
using Yarp.ReverseProxy.Model;

namespace Lycoris.Yarp.Nacos.Extensions
{
    /// <summary>
    /// 基于 Nacos 实例权重的加权随机负载均衡策略。
    /// 从目标实例的元数据中读取 Weight 值，按权重比例随机分配请求。
    /// 无权重元数据时默认权重为 1.0。
    /// </summary>
    public sealed class WeightLoadBalancingPolicy : ILoadBalancingPolicy
    {
        /// <summary>
        /// 负载均衡策略名称
        /// </summary>
        public string Name { get => YarpNacosConstants.WeightLoadBalancingPolicy; }

        /// <summary>
        /// 从可用目标中按权重随机选择一个实例
        /// </summary>
        /// <param name="context">当前 HTTP 请求上下文</param>
        /// <param name="cluster">当前集群状态</param>
        /// <param name="availableDestinations">可用的目标实例列表</param>
        /// <returns>选中的目标实例，无可用实例时返回 null</returns>
        public DestinationState? PickDestination(HttpContext context, ClusterState cluster, IReadOnlyList<DestinationState> availableDestinations)
        {
            // 提取每个实例的权重
            var weights = GetDestinationWeights(availableDestinations);

            // 基于权重选择实例
            var loadBalancer = new LoadBalancer(weights);

            var destinationIndex = loadBalancer.SelectInstance();

            return availableDestinations[destinationIndex];
        }

        /// <summary>
        /// 从目标实例的元数据中提取权重信息。
        /// 遍历所有可用实例，从 Metadata 中找到 Weight Key 对应的值。
        /// 无权重元数据时默认权重为 1.0。
        /// </summary>
        /// <param name="availableDestinations">可用的目标实例列表</param>
        /// <returns>实例索引到权重的映射字典</returns>
        private static Dictionary<int, double> GetDestinationWeights(IReadOnlyList<DestinationState> availableDestinations)
        {
            var dic = new Dictionary<int, double>();

            for (int i = 0; i < availableDestinations.Count; i++)
            {
                var item = availableDestinations[i];
                if (item.Model.Config.Metadata == null || !item.Model.Config.Metadata.Any(x => x.Key == YarpNacosConstants.InstanceWeight))
                {
                    dic.Add(i, 1.0);
                    continue;
                }

                // 从元数据中取出权重值并安全解析
                var weightValue = item.Model.Config.Metadata.SingleOrDefault(x => x.Key == YarpNacosConstants.InstanceWeight).Value;
                var weight = double.TryParse(weightValue, out var w) ? w : 1.0;

                dic.Add(i, weight);
            }

            return dic;
        }
    }
}
