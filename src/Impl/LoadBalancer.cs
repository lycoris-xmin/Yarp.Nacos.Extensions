namespace Lycoris.Yarp.Nacos.Extensions.Impl
{
    /// <summary>
    /// 基于权重的加权随机负载均衡算法实现。
    /// 权重越大的实例被选中的概率越高。
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
        /// <param name="Instances">实例索引到权重的映射，Key 为实例索引，Value 为权重值</param>
        /// <exception cref="ArgumentException">当实例列表非空但所有权重之和为零时抛出</exception>
        public LoadBalancer(Dictionary<int, double>? Instances)
        {
            this.Instances = Instances ?? new Dictionary<int, double>();
            this._Random = new Random();

            if (this.Instances.Count > 0 && this.Instances.Sum(x => x.Value) == 0)
                throw new ArgumentException("所有权重都为零");
        }

        /// <summary>
        /// 使用加权随机算法选择一个实例索引。
        /// 算法：生成 [0, totalWeight) 的随机数，按权重累加遍历，随机数落入哪个区间就选中哪个实例。
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

            // 浮点精度保护：返回最后一个实例
            return this.Instances.LastOrDefault().Key;
        }
    }
}
