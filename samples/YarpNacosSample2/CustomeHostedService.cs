using Lycoris.Yarp.Nacos.Extensions;
using Lycoris.Yarp.Nacos.Extensions.Logging;
using Lycoris.Yarp.Nacos.Extensions.Options;
using Microsoft.Extensions.Options;

namespace YarpNacosSample2
{
    /// <summary>
    /// 自定义服务心跳后台任务示例。
    /// 演示如何实现自定义的服务上下线检测逻辑。
    /// </summary>
    public class CustomeHostedService : BackgroundService
    {
        private readonly IYarpNacosLogger _logger;
        private readonly IYarpNacosStore _store;
        private readonly YarpNacosOptions _options;

        /// <summary>
        /// 初始化心跳服务
        /// </summary>
        public CustomeHostedService(IYarpNacosLoggerFactory factory, IYarpNacosStore store, IOptions<YarpNacosOptions> options)
        {
            _logger = factory.CreateLogger<CustomeHostedService>();
            _store = store;
            _options = options.Value;
        }

        /// <summary>
        /// 执行自定义心跳循环
        /// </summary>
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
                    _logger.Error("custom heartbeat error", ex);
                }

                await Task.Delay(_options.NacosServicesHeartbeat, stoppingToken);
            } while (!stoppingToken.IsCancellationRequested);
        }
    }
}
