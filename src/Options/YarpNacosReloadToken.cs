using Microsoft.Extensions.Primitives;

namespace Lycoris.Yarp.Nacos.Extensions.Options
{
    /// <summary>
    /// Yarp Nacos 配置重载令牌，实现 <see cref="IChangeToken"/> 接口。
    /// 基于 CancellationTokenSource 实现，用于在配置发生变更时通知 Yarp 重新加载配置。
    /// 每次调用 OnReload 会取消内部 CTS，触发所有注册的回调。
    /// </summary>
    public sealed class YarpNacosReloadToken : IChangeToken
    {
        /// <summary>内部取消令牌源，用于触发变更通知</summary>
        private CancellationTokenSource _cts = new();

        /// <summary>
        /// 指示此令牌是否支持主动回调通知，始终返回 true
        /// </summary>
        public bool ActiveChangeCallbacks => true;

        /// <summary>
        /// 指示配置是否已发生变更
        /// </summary>
        public bool HasChanged => _cts.IsCancellationRequested;

        /// <summary>
        /// 注册配置变更回调。当 OnReload 被调用时执行 callback。
        /// </summary>
        /// <param name="callback">变更时执行的回调委托</param>
        /// <param name="state">回调状态对象，会传递给回调</param>
        /// <returns>用于取消注册的 IDisposable 对象</returns>
        public IDisposable RegisterChangeCallback(Action<object?> callback, object? state) => _cts.Token.Register(callback, state);

        /// <summary>
        /// 触发配置重载，取消当前内部 CTS 并通知所有已注册的回调。
        /// 注意：调用后此令牌实例即失效，后续变更需创建新令牌。
        /// </summary>
        public void OnReload() => _cts.Cancel();
    }
}
