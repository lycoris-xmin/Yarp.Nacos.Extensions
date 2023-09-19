using Lycoris.Base.Logging;
using Lycoris.Yarp.Nacos.Extensions.Options;
using Microsoft.Extensions.Primitives;
using System.Diagnostics.CodeAnalysis;
using Yarp.ReverseProxy.Configuration;

namespace Lycoris.Yarp.Nacos.Extensions.Impl
{
    /// <summary>
    /// 
    /// </summary>
    public sealed class YarpProxyConfigProvider : IProxyConfigProvider, IDisposable
    {
        private readonly object _lockObject = new();
        private readonly ILycorisLogger _logger;
        private readonly IYarpNacosStore _store;

        private YarpNacosProxyConfig? _config;
        private CancellationTokenSource? _changeToken;
        private bool _disposed;
        private IDisposable? _subscription;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="factory"></param>
        /// <param name="store"></param>
        public YarpProxyConfigProvider(ILycorisLoggerFactory factory, IYarpNacosStore store)
        {
            _logger = factory.CreateLogger<YarpNacosStore>();
            _store = store;
        }

        /// <summary>
        /// 
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
        /// 
        /// </summary>
        /// <returns></returns>
        public IProxyConfig GetConfig()
        {
            // First time load
            if (_config == null)
            {
                _subscription = ChangeToken.OnChange(_store.GetReloadToken, UpdateConfig);
                UpdateConfig();
            }

            return _config;
        }

        /// <summary>
        /// 
        /// </summary>
        [MemberNotNull(nameof(_config))]
        private void UpdateConfig()
        {
            // 防止重叠更新。
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