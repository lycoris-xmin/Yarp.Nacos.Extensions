using Microsoft.Extensions.Primitives;

namespace Lycoris.Yarp.Nacos.Extensions.Options
{
    /// <summary>
    /// Yarp Nacos 配置重载令牌，实现 <see cref="IChangeToken"/> 接口。
    /// 用于在配置发生变更时通知 Yarp 重新加载配置
    /// </summary>
    public sealed class YarpNacosReloadToken : IChangeToken
    {
        private CancellationTokenSource _cts = new();

        /// <summary>
        /// 指示此令牌是否支持主动回调通知
        /// </summary>
        public bool ActiveChangeCallbacks => true;

        /// <summary>
        /// 指示配置是否已发生变更
        /// </summary>
        public bool HasChanged => _cts.IsCancellationRequested;

        /// <summary>
        /// 注册配置变更回调
        /// </summary>
        /// <param name="callback">变更时执行的回调</param>
        /// <param name="state">回调状态对象</param>
        /// <returns>用于取消注册的 disposable 对象</returns>
        public IDisposable RegisterChangeCallback(Action<object?> callback, object? state) => _cts.Token.Register(callback, state);

        /// <summary>
        /// 触发配置重载，取消当前令牌并通知所有注册的回调
        /// </summary>
        public void OnReload() => _cts.Cancel();
    }
}
