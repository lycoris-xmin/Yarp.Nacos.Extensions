using Nacos.V2.Naming.Dtos;

namespace Lycoris.Yarp.Nacos.Extensions
{
    /// <summary>
    /// Nacos 服务变更监听器接口。
    /// 实现此接口以接收服务上线、下线、实例变更事件的回调通知，
    /// 可用于自定义告警、日志记录、指标上报等场景。
    /// 通过 <see cref="YarpNacosPaoxyBuilder.AddServiceChangeListener{T}"/> 注册。
    /// </summary>
    public interface IYarpNacosServiceChangeListener
    {
        /// <summary>
        /// 新服务上线回调。
        /// 当心跳检测或 Nacos 事件推送发现新的服务集群时触发。
        /// </summary>
        /// <param name="groupName">Nacos 群组名称</param>
        /// <param name="serviceName">Nacos 服务名称</param>
        /// <param name="instances">当前健康的实例列表</param>
        /// <param name="cancellationToken">取消令牌</param>
        Task OnServiceOnlineAsync(string groupName, string serviceName, List<Instance> instances, CancellationToken cancellationToken);

        /// <summary>
        /// 服务下线回调。
        /// 当心跳检测发现服务集群已从 Nacos 移除或所有实例不健康时触发。
        /// </summary>
        /// <param name="groupName">Nacos 群组名称</param>
        /// <param name="serviceName">Nacos 服务名称</param>
        /// <param name="cancellationToken">取消令牌</param>
        Task OnServiceOfflineAsync(string groupName, string serviceName, CancellationToken cancellationToken);

        /// <summary>
        /// 服务实例变更回调。
        /// 当 Nacos 推送服务实例发生变化（实例扩缩容、权重变更、元数据变更等）时触发。
        /// </summary>
        /// <param name="groupName">Nacos 群组名称</param>
        /// <param name="serviceName">Nacos 服务名称</param>
        /// <param name="instances">变更后的健康实例列表</param>
        /// <param name="cancellationToken">取消令牌</param>
        Task OnServiceChangedAsync(string groupName, string serviceName, List<Instance> instances, CancellationToken cancellationToken);
    }
}
