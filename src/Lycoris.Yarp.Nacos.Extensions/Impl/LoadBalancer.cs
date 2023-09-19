namespace Lycoris.Yarp.Nacos.Extensions.Impl
{
    /// <summary>
    /// 
    /// </summary>
    public class LoadBalancer
    {
        /// <summary>
        /// 
        /// </summary>
        public Dictionary<int, double> Instances { get; set; }

        /// <summary>
        /// 
        /// </summary>
        protected Random _Random;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="Instances"></param>
        public LoadBalancer(Dictionary<int, double>? Instances)
        {
            this.Instances = Instances ?? new Dictionary<int, double>();
            this._Random = new Random();

            if (this.Instances.Sum(x => x.Value) == 0)
                throw new ArgumentException("所有权重都为零");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
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
