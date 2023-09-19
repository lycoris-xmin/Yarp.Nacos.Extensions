using Lycoris.Base.Extensions;
using Lycoris.Yarp.Nacos.Extensions.Impl;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.LoadBalancing;
using Yarp.ReverseProxy.Model;

namespace Lycoris.Yarp.Nacos.Extensions
{
    /// <summary>
    /// 
    /// </summary>
    public sealed class WeightLoadBalancingPolicy : ILoadBalancingPolicy
    {
        /// <summary>
        /// 
        /// </summary>
        public string Name { get => YarpNacosConstants.WeightLoadBalancingPolicy; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        /// <param name="cluster"></param>
        /// <param name="availableDestinations"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public DestinationState? PickDestination(HttpContext context, ClusterState cluster, IReadOnlyList<DestinationState> availableDestinations)
        {
            // 
            var weights = GetDestinationWeights(availableDestinations);

            //
            var loadBalancer = new LoadBalancer(weights);

            //
            var destinationIndex = loadBalancer.SelectInstance();

            return availableDestinations[destinationIndex];
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="availableDestinations"></param>
        /// <returns></returns>
        private static Dictionary<int, double> GetDestinationWeights(IReadOnlyList<DestinationState> availableDestinations)
        {
            var dic = new Dictionary<int, double>();

            for (int i = 0; i < availableDestinations.Count; i++)
            {
                var item = availableDestinations[i];
                if (item.Model.Config.Metadata == null || !item.Model.Config.Metadata.Any(x => x.Key == YarpNacosConstants.InstanceWeight))
                {
                    dic.Add(i, 0);
                    continue;
                }

                var weightValue = item.Model.Config.Metadata.SingleOrDefault(x => x.Key == YarpNacosConstants.InstanceWeight).Value;
                var weight = weightValue.ToTryDouble() ?? 0;

                dic.Add(i, weight);
            }

            return dic;
        }



    }
}
