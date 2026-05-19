using Lycoris.Yarp.Nacos.Extensions.Impl;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.LoadBalancing;
using Yarp.ReverseProxy.Model;

namespace Lycoris.Yarp.Nacos.Extensions
{
    /// <summary>
    /// 基于 Nacos 实例权重的负载均衡策略。
    /// 根据目标实例的 Weight 元数据按权重比例分配请求。
    /// </summary>
    public sealed class WeightLoadBalancingPolicy : ILoadBalancingPolicy
    {
        /// <summary>
        /// 负载均衡策略名称
        /// </summary>
        public string Name { get => YarpNacosConstants.WeightLoadBalancingPolicy; }

        /// <summary>
        /// 从可用目标中选择一个目标实例，按权重进行随机选择
        /// </summary>
        /// <param name="context">当前 HTTP 请求上下文</param>
        /// <param name="cluster">当前集群状态</param>
        /// <param name="availableDestinations">可用的目标实例列表</param>
        /// <returns>选中的目标实例，没有可用实例时返回 null</returns>
        public DestinationState? PickDestination(HttpContext context, ClusterState cluster, IReadOnlyList<DestinationState> availableDestinations)
        {
            var weights = GetDestinationWeights(availableDestinations);

            var loadBalancer = new LoadBalancer(weights);

            var destinationIndex = loadBalancer.SelectInstance();

            return availableDestinations[destinationIndex];
        }

        /// <summary>
        /// 从目标实例中提取权重信息，无权重元数据时默认权重为 1.0
        /// </summary>
        /// <param name="availableDestinations">可用的目标实例列表</param>
        /// <returns>实例索引到权重的映射</returns>
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

                var weightValue = item.Model.Config.Metadata.SingleOrDefault(x => x.Key == YarpNacosConstants.InstanceWeight).Value;
                var weight = double.TryParse(weightValue, out var w) ? w : 1.0;

                dic.Add(i, weight);
            }

            return dic;
        }
    }
}
