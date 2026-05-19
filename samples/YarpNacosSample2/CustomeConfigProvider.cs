using Lycoris.Yarp.Nacos.Extensions;
using Lycoris.Yarp.Nacos.Extensions.Logging;
using Lycoris.Yarp.Nacos.Extensions.Options;
using Microsoft.Extensions.Primitives;
using System.Diagnostics.CodeAnalysis;
using Yarp.ReverseProxy.Configuration;

namespace YarpNacosSample2
{
    /// <summary>
    /// 自定义 Yarp 代理配置提供者示例。
    /// 演示如何接管配置的加载和热重载机制。
    /// </summary>
    public class CustomeConfigProvider : IProxyConfigProvider, IDisposable
    {
        private readonly object _lockObject = new();
        private readonly IYarpNacosLogger _logger;
        private readonly IYarpNacosStore _store;

        private YarpNacosProxyConfig? _config;
        private CancellationTokenSource? _changeToken;
        private bool _disposed;
        private IDisposable? _subscription;

        /// <summary>
        /// 初始化配置提供者
        /// </summary>
        public CustomeConfigProvider(IYarpNacosLoggerFactory factory, IYarpNacosStore store)
        {
            _logger = factory.CreateLogger<CustomeConfigProvider>();
            _store = store;
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _subscription?.Dispose();
                _changeToken?.Dispose();
                _disposed = true;
            }
        }

        /// <summary>
        /// 获取当前代理配置，首次调用时初始化并订阅变更
        /// </summary>
        public IProxyConfig GetConfig()
        {
            if (_config == null)
            {
                _subscription = ChangeToken.OnChange(_store.GetReloadToken, UpdateConfig);
                UpdateConfig();
            }

            return _config;
        }

        /// <summary>
        /// 从 Store 重新加载配置并通知 Yarp
        /// </summary>
        [MemberNotNull(nameof(_config))]
        private void UpdateConfig()
        {
            lock (_lockObject)
            {
                YarpNacosProxyConfig? newConfig = null;
                try
                {
                    newConfig = _store.GetConfigAsync().ConfigureAwait(false).GetAwaiter().GetResult() as YarpNacosProxyConfig;
                }
                catch (Exception ex)
                {
                    _logger?.Error("update yarp configuration error", ex);

                    if (_config == null)
                        throw;

                    return;
                }

                if (newConfig == null)
                    throw new ArgumentNullException(nameof(newConfig));

                var oldToken = _changeToken;
                _changeToken = new CancellationTokenSource();
                newConfig.ChangeToken = new CancellationChangeToken(_changeToken.Token);
                _config = newConfig;

                try
                {
                    oldToken?.Cancel(throwOnFirstException: false);
                }
                catch (Exception ex)
                {
                    _logger?.Error("cancel old changeToken error", ex);
                }
            }
        }
    }
}
