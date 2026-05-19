using Microsoft.Extensions.Logging;

namespace Lycoris.Yarp.Nacos.Extensions.Logging
{
    internal sealed class DefaultYarpNacosLogger : IYarpNacosLogger
    {
        private readonly ILogger _logger;

        public DefaultYarpNacosLogger(ILogger logger) => _logger = logger;

        public void Info(string message) => _logger.LogInformation(message);
        public void Warn(string message) => _logger.LogWarning(message);
        public void Error(string message, Exception? ex = null) => _logger.LogError(ex, message);
    }
}
