using Lycoris.Yarp.Nacos.Extensions;
using Lycoris.Yarp.Nacos.Extensions.Options;
using Microsoft.Extensions.Options;

namespace YarpNacosSample2
{
    public class CustomeHostedService : BackgroundService
    {
        private readonly IYarpLogger _logger;
        private readonly IYarpNacosStore _store;
        private readonly YarpNacosOptions _options;

        public CustomeHostedService(IYarpLoggerFactory factory, IYarpNacosStore store, IOptions<YarpNacosOptions> options)
        {
            _logger = factory.CreateLogger<CustomeHostedService>();
            _store = store;
            _options = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            do
            {
                try
                {
                    // do something
                }
                catch (Exception ex)
                {
                    _logger.Error("", ex);
                }

                await Task.Delay(_options.NacosServicesHeartbeat, stoppingToken);
            } while (true);
        }
    }
}
