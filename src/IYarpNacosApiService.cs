namespace Lycoris.Yarp.Nacos.Extensions
{
    /// <summary>
    /// Yarp Nacos API 服务标记接口。
    /// 实现此接口的类型表示一个网关聚合 API，内部可调用 Nacos 注册中心中的多个微服务并组装返回结果。
    /// 通过 <see cref="YarpNacosPaoxyBuilder.AddApi{TInterface, TImpl}(Action{Options.NacosApiOptions}?)"/> 注册到 DI 容器。
    /// </summary>
    public interface IYarpNacosApiService
    {

    }
}
