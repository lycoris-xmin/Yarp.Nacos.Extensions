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
        /// <summary>防止 UpdateConfig 重叠执行的锁对象</summary>
        private readonly object _lockObject = new();
        /// <summary>日志记录器</summary>
        private readonly IYarpNacosLogger _logger;
        /// <summary>Nacos 状态管理器</summary>
        private readonly IYarpNacosStore _store;

        /// <summary>当前代理配置，null 表示尚未初始化</summary>
        private YarpNacosProxyConfig? _config;
        /// <summary>当前变更令牌源</summary>
        private CancellationTokenSource? _changeToken;
        /// <summary>是否已释放</summary>
        private bool _disposed;
        /// <summary>ChangeToken 变更订阅句柄</summary>
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
        /// 释放资源，取消 ChangeToken 订阅
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
        /// 更新配置：从 Store 重新加载配置并通知 Yarp 变更。
        /// 加载成功后取消旧 ChangeToken、创建新 Token，Yarp 通过新 Token 感知配置已更新。
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

                    // 首次加载失败时抛出，阻止应用启动
                    if (_config == null)
                        throw;

                    return;
                }

                if (newConfig == null)
                    throw new ArgumentNullException(nameof(newConfig));

                // 取消旧 Token 通知订阅者配置已过期
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
