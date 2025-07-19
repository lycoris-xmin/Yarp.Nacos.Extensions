using Microsoft.Extensions.Logging;

namespace Lycoris.Yarp.Nacos.Extensions.Impl
{
    /// <summary>
    /// 
    /// </summary>
    public class YarpLoggerFactory : IYarpLoggerFactory
    {
        private readonly ILoggerFactory _factory;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="factory"></param>
        public YarpLoggerFactory(ILoggerFactory factory)
        {
            _factory = factory;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public IYarpLogger CreateLogger<T>()
        {
            var logger = _factory.CreateLogger<T>();
            return new YarpLogger(logger);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public IYarpLogger CreateLogger(Type type)
        {
            var logger = _factory.CreateLogger(type);
            return new YarpLogger(logger);
        }
    }
}
