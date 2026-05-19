using Microsoft.Extensions.Logging;

namespace Lycoris.Yarp.Nacos.Extensions.Logging
{
    internal sealed class DefaultYarpNacosLoggerFactory : IYarpNacosLoggerFactory
    {
        private readonly ILoggerFactory _factory;

        public DefaultYarpNacosLoggerFactory(ILoggerFactory factory) => _factory = factory;

        public IYarpNacosLogger CreateLogger<T>() => new DefaultYarpNacosLogger(_factory.CreateLogger<T>());
    }
}
