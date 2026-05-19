namespace Lycoris.Yarp.Nacos.Extensions.Impl
{
    /// <summary>
    /// 基于权重的负载均衡算法实现。
    /// 使用加权随机算法（Weighted Random），按实例权重比例分配请求。
    /// </summary>
    public class LoadBalancer
    {
        /// <summary>
        /// 实例索引到权重的映射字典
        /// </summary>
        public Dictionary<int, double> Instances { get; set; }

        /// <summary>
        /// 随机数生成器
        /// </summary>
        protected Random _Random;

        /// <summary>
        /// 初始化权重负载均衡器
        /// </summary>
        /// <param name="Instances">实例索引到权重的映射</param>
        /// <exception cref="ArgumentException">当所有权重均为零时抛出</exception>
        public LoadBalancer(Dictionary<int, double>? Instances)
        {
            this.Instances = Instances ?? new Dictionary<int, double>();
            this._Random = new Random();

            if (this.Instances.Count > 0 && this.Instances.Sum(x => x.Value) == 0)
                throw new ArgumentException("所有权重都为零");
        }

        /// <summary>
        /// 使用加权随机算法选择一个实例索引。
        /// 权重越大的实例被选中的概率越高。
        /// </summary>
        /// <returns>选中的实例索引</returns>
        public virtual int SelectInstance()
        {
            var totalWeight = this.Instances.Sum(x => x.Value);

            var randomNumber = this._Random.NextDouble() * totalWeight;
            var accumulatedWeight = 0D;

            foreach (var item in this.Instances)
            {
                accumulatedWeight += item.Value;
                if (randomNumber < accumulatedWeight)
                    return item.Key;
            }

            return this.Instances.LastOrDefault().Key;
        }
    }
}
