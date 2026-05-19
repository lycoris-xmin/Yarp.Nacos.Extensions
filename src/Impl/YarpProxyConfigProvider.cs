using Lycoris.Yarp.Nacos.Extensions.Logging;
using Lycoris.Yarp.Nacos.Extensions.Options;
using Microsoft.Extensions.Primitives;
using System.Diagnostics.CodeAnalysis;
using Yarp.ReverseProxy.Configuration;

namespace Lycoris.Yarp.Nacos.Extensions.Impl
{
    /// <summary>
    /// Yarp 代理配置提供者，实现 <see cref="IProxyConfigProvider"/> 接口。
    /// 负责从 <see cref="IYarpNacosStore"/> 加载配置并通过 ChangeToken 机制感知配置变更。
    /// </summary>
    public sealed class YarpProxyConfigProvider : IProxyConfigProvider, IDisposable
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
        /// <param name="factory">日志工厂</param>
        /// <param name="store">Nacos 状态管理器</param>
        public YarpProxyConfigProvider(IYarpNacosLoggerFactory factory, IYarpNacosStore store)
        {
            _logger = factory.CreateLogger<YarpNacosStore>();
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
        /// 获取当前代理配置。首次调用时初始化配置并订阅变更通知
        /// </summary>
        /// <returns>当前代理配置</returns>
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
        /// 更新配置：从 Store 重新加载配置并通知 Yarp 变更
        /// </summary>
        [MemberNotNull(nameof(_config))]
        private void UpdateConfig()
        {
            // 防止重叠更新
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
