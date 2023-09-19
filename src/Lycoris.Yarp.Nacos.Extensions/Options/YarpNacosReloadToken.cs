using Microsoft.Extensions.Primitives;

namespace Lycoris.Yarp.Nacos.Extensions.Options
{
    /// <summary>
    /// 
    /// </summary>
    public sealed class YarpNacosReloadToken : IChangeToken
    {
        private CancellationTokenSource _cts = new();

        /// <summary>
        /// 
        /// </summary>
        public bool ActiveChangeCallbacks => true;

        /// <summary>
        /// 
        /// </summary>
        public bool HasChanged => _cts.IsCancellationRequested;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="state"></param>
        /// <returns></returns>
        public IDisposable RegisterChangeCallback(Action<object?> callback, object state) => _cts.Token.Register(callback, state);

        /// <summary>
        /// 
        /// </summary>
        public void OnReload() => _cts.Cancel();
    }
}