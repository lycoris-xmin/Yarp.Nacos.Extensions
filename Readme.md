# Lycoris.Yarp.Nacos.Extensions

[![NuGet](https://img.shields.io/nuget/v/Lycoris.Yarp.Nacos.Extensions.svg)](https://www.nuget.org/packages/Lycoris.Yarp.Nacos.Extensions)
[![Target Framework](https://img.shields.io/badge/target-.NET%208-blue)](https://dotnet.microsoft.com/)

基于 **Yarp.ReverseProxy** 的 **Nacos** 服务发现扩展，让 Yarp 网关自动从 Nacos 注册中心发现微服务，监控服务上下线并实时更新反向代理配置。

## 功能特性

- 自动从 Nacos 注册中心发现服务并生成 Yarp 反向代理配置
- 支持多群组、多服务的动态路由映射
- 实时监听 Nacos 服务上下线，自动更新代理配置
- 支持基于权重的负载均衡策略
- 支持被动健康检查（传输失败率策略）
- 完全可扩展：支持自定义路由规则、集群配置、状态管理、日志工厂
- 支持 `appsettings.json` 配置文件绑定
- 内置日志抽象，用户可自定义日志格式对接日志切割分片系统

## 安装

```bash
dotnet add package Lycoris.Yarp.Nacos.Extensions
```

## 快速开始

### 1. 配置 Nacos

在 `Program.cs` 中注册 Nacos 服务和 Yarp Nacos 扩展：

```csharp
using Lycoris.Yarp.Nacos.Extensions;
using Nacos.V2.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// 注册 Nacos 配置服务
builder.Services.AddNacosV2Config(opt =>
{
    opt.ServerAddresses = new List<string>() { "http://localhost:8848" };
    opt.Namespace = "your-namespace";
    opt.UserName = "your-username";
    opt.Password = "your-password";
    opt.ConfigUseRpc = true;
});

// 注册 Nacos 命名服务
builder.Services.AddNacosV2Naming(opt =>
{
    opt.ServerAddresses = new List<string>() { "http://localhost:8848" };
    opt.Namespace = "your-namespace";
    opt.UserName = "your-username";
    opt.Password = "your-password";
    opt.NamingUseRpc = true;
});

// 注册 Yarp Nacos 扩展
builder.Services.AddYarpNacosPaoxy(builder =>
{
    builder.OptionBuilder(opt =>
    {
        opt.GroupNameList = new List<string>() { "DEFAULT_GROUP" };
    });
});

var app = builder.Build();

app.MapReverseProxy(proxyPipeline =>
{
    proxyPipeline.UsePassiveHealthChecks();
});

app.Run();
```

### 2. 默认路由规则

扩展默认生成的路由规则为：

```
http://网关地址/Nacos群组名/Nacos服务名/接口路径
      ↓
http://实例IP:实例端口/接口路径
```

例如 Nacos 上注册了 `DEFAULT_GROUP` 群组下的 `user-service` 微服务，则：

```
GET http://localhost:5000/DEFAULT_GROUP/user-service/api/users
→ 反向代理到 http://192.168.1.10:8080/api/users
```

### 3. 通过配置文件配置

也可以通过 `appsettings.json` 配置：

```json
{
  "YarpNacos": {
    "GroupNameList": ["DEFAULT_GROUP", "API_GROUP"],
    "PreCount": 100,
    "NacosServicesHeartbeat": 5,
    "LoadBalancingPolicyName": "WeightLoadBalancingPolicy"
  }
}
```

```csharp
// 纯配置文件方式
builder.Services.AddYarpNacosPaoxy(configuration.GetSection("YarpNacos"));

// 配置文件 + 代码覆盖
builder.Services.AddYarpNacosPaoxy(configuration.GetSection("YarpNacos"), builder =>
{
    builder.AddWeightLoadBalancingPolicy();
});
```

## 进阶用法

### 自定义路由映射规则

实现 `IYarpNacosPaoxyConfigMapper` 接口自定义路由和集群的生成逻辑：

```csharp
public class MyConfigMapper : IYarpNacosPaoxyConfigMapper
{
    public RouteConfig CreateRouteConfig(string clusterId, string groupName, string serviceName)
    {
        // 自定义路由匹配规则
        return new RouteConfig
        {
            RouteId = $"{clusterId}-route",
            ClusterId = clusterId,
            Match = new RouteMatch
            {
                Path = $"/api/{serviceName}/{{**catch-all}}"
            }
        };
    }

    public ClusterConfig CreateClusterConfig(string clusterId,
        IReadOnlyDictionary<string, DestinationConfig> destinations)
    {
        // 自定义集群配置（负载策略、健康检查等）
        return new ClusterConfig
        {
            ClusterId = clusterId,
            LoadBalancingPolicy = LoadBalancingPolicies.RoundRobin,
            Destinations = destinations
        };
    }

    public Dictionary<string, DestinationConfig> CreateDestinationConfig(List<Instance> instances)
    {
        // 自定义目标地址生成逻辑
        var destinations = new Dictionary<string, DestinationConfig>();
        foreach (var instance in instances.Where(x => x.Healthy && x.Enabled))
        {
            destinations.Add($"{instance.Ip}:{instance.Port}", new DestinationConfig
            {
                Address = $"http://{instance.Ip}:{instance.Port}"
            });
        }
        return destinations;
    }
}

// 注册自定义映射器
builder.Services.AddYarpNacosPaoxy(builder =>
{
    builder.OptionBuilder(opt => { ... });
    builder.AddYarpNacosPaoxyConfigMapper<MyConfigMapper>();
});
```

> 如果只需要微调默认映射规则的部分行为，可以继承 `YarpNacosPaoxyConfigMapper` 并覆盖对应的 `virtual` 方法。

### 基于权重的负载均衡

```csharp
builder.Services.AddYarpNacosPaoxy(builder =>
{
    builder.OptionBuilder(opt => { ... });
    builder.AddWeightLoadBalancingPolicy();
});
```

启用后，扩展将根据 Nacos 实例的 `Weight` 属性按权重比例分配请求。

### 自定义负载均衡策略

```csharp
builder.Services.AddYarpNacosPaoxy(builder =>
{
    builder.OptionBuilder(opt => { ... });
    builder.AddLoadBalancingPolicy<MyPolicy>("my-policy");
});
```

### 自定义服务心跳任务

```csharp
builder.Services.AddYarpNacosPaoxy(builder =>
{
    builder.OptionBuilder(opt => { ... });
    builder.AddNacosServiceHeart<MyHeartbeatService>();
});
```

### 自定义日志输出

实现 `IYarpNacosLoggerFactory` 和 `IYarpNacosLogger` 接口，将内部日志对接到你自己的日志系统：

```csharp
public class MyLoggerFactory : IYarpNacosLoggerFactory
{
    public IYarpNacosLogger CreateLogger<T>()
    {
        // 返回你实现的 IYarpNacosLogger
        return new MyLogger(typeof(T).Name);
    }
}

public class MyLogger : IYarpNacosLogger
{
    private readonly string _category;
    public MyLogger(string category) => _category = category;

    public void Info(string message)
        => WriteLog("INFO", message);

    public void Warn(string message)
        => WriteLog("WARN", message);

    public void Error(string message, Exception? ex = null)
        => WriteLog("ERROR", $"{message} {ex?.Message}");

    private void WriteLog(string level, string message)
    {
        // 按你的格式输出，方便日志切割分片
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] [{_category}] {message}");
    }
}

// 注册
builder.Services.AddYarpNacosPaoxy(builder =>
{
    builder.OptionBuilder(opt => { ... });
    builder.AddYarpNacosLoggerFactory<MyLoggerFactory>();
});
```

## 可扩展组件一览

| 接口 | 用途 | 对应注册方法 |
|------|------|-------------|
| `IYarpNacosPaoxyConfigMapper` | 自定义路由/集群配置映射规则 | `AddYarpNacosPaoxyConfigMapper<T>()` |
| `IYarpNacosStore` | 自定义状态管理（缓存、订阅） | `AddYarpNacosStore<T>()` |
| `IProxyConfigProvider` | 自定义 Yarp 配置提供者 | `AddYarpProxyConfigProvider<T>()` |
| `IYarpNacosLoggerFactory` | 自定义日志输出格式 | `AddYarpNacosLoggerFactory<T>()` |
| `IHostedService` | 自定义服务心跳任务 | `AddNacosServiceHeart<T>()` |
| `ILoadBalancingPolicy` | 自定义负载均衡策略 | `AddLoadBalancingPolicy<T>()` |

## 配置选项说明

| 选项 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `GroupNameList` | `List<string>` | `[]` | 需要监听的 Nacos 服务群组列表 |
| `PreCount` | `int` | `50` | 拉取服务列表的每页数量 |
| `NacosServicesHeartbeat` | `int` | `5` | 心跳检测间隔（秒） |
| `LoadBalancingPolicyName` | `string?` | `null` | 负载均衡策略名称 |

## License

Apache-2.0
