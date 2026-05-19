# Lycoris.Yarp.Nacos.Extensions

[![NuGet](https://img.shields.io/nuget/v/Lycoris.Yarp.Nacos.Extensions.svg)](https://www.nuget.org/packages/Lycoris.Yarp.Nacos.Extensions)
[![Target Framework](https://img.shields.io/badge/target-.NET%208-blue)](https://dotnet.microsoft.com/)

Yarp 反向代理的 Nacos 服务发现扩展。自动从 Nacos 注册中心发现微服务，监听服务上下线并实时更新反向代理配置。

## 功能特性

- **服务发现** — 自动拉取 Nacos 注册中心的服务列表，生成 Yarp 路由和集群配置
- **实时监听** — 通过心跳轮询 + Nacos 事件推送双重机制，监控服务上下线，支持自定义回调
- **权重负载均衡** — 基于 Nacos 实例 Weight 的加权随机策略
- **被动健康检查** — 传输失败率策略，自动隔离不健康实例
- **多 Namespace** — 不同群组可映射到不同 Nacos 命名空间
- **元数据过滤** — 按实例元数据标签筛选需要代理的服务
- **优雅关闭** — 应用停止时自动清理所有 Nacos 监听订阅
- **可扩展** — 路由规则、集群配置、状态管理、日志工厂、负载均衡策略均可替换
- **配置文件支持** — 可通过 `appsettings.json` 绑定选项
- **链路追踪** — 可接入 OpenTelemetry、SkyWalking 等追踪系统，自动传播 trace 上下文
- **自定义日志** — 内置日志抽象，可对接任意日志系统实现切割分片

## 安装

```bash
dotnet add package Lycoris.Yarp.Nacos.Extensions
```

## 快速开始

### 1. 基本用法

```csharp
using Lycoris.Yarp.Nacos.Extensions;
using Nacos.V2.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Nacos 配置服务
builder.Services.AddNacosV2Config(opt =>
{
    opt.ServerAddresses = new List<string> { "http://localhost:8848" };
    opt.Namespace = "your-namespace";
    opt.UserName = "your-username";
    opt.Password = "your-password";
    opt.ConfigUseRpc = true;
});

// Nacos 命名服务
builder.Services.AddNacosV2Naming(opt =>
{
    opt.ServerAddresses = new List<string> { "http://localhost:8848" };
    opt.Namespace = "your-namespace";
    opt.UserName = "your-username";
    opt.Password = "your-password";
    opt.NamingUseRpc = true;
});

// Yarp + Nacos 扩展
builder.Services.AddYarpNacosPaoxy(builder =>
{
    builder.OptionBuilder(opt =>
    {
        opt.GroupNameList = new List<string> { "DEFAULT_GROUP" };
    });
});

var app = builder.Build();

app.MapReverseProxy(proxyPipeline =>
{
    proxyPipeline.UsePassiveHealthChecks();
});

app.Run();
```

### 2. 路由规则

扩展默认生成的路由规则：

```
http://网关地址/{群组名}/{服务名}/{接口路径}
         ↓
http://{实例IP}:{实例端口}/{接口路径}
```

例如 Nacos 上注册了 `DEFAULT_GROUP` 群组下的 `user-service`：

```
GET http://localhost:5000/DEFAULT_GROUP/user-service/api/users
→ http://192.168.1.10:8080/api/users
```

### 3. appsettings.json 配置

```json
{
  "YarpNacos": {
    "GroupNameList": ["DEFAULT_GROUP", "API_GROUP"],
    "PreCount": 100,
    "NacosServicesHeartbeat": 5,
    "LoadBalancingPolicyName": "RoundRobin",
    "GroupNamespaceMap": {
      "API_GROUP": "namespace-api-xxx"
    },
    "InstanceMetadataFilter": {
      "env": "prod"
    }
  }
}
```

```csharp
// 纯配置文件
builder.Services.AddYarpNacosPaoxy(configuration.GetSection("YarpNacos"));

// 配置文件 + 代码覆盖（代码优先级更高）
builder.Services.AddYarpNacosPaoxy(configuration.GetSection("YarpNacos"), builder =>
{
    builder.AddWeightLoadBalancingPolicy();
});
```

## 配置选项

| 选项 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `GroupNameList` | `List<string>` | `[]` | 需要监听的 Nacos 服务群组列表 |
| `PreCount` | `int` | `50` | 拉取服务列表的每页数量 |
| `NacosServicesHeartbeat` | `int` | `5` | 心跳检测间隔，单位秒 |
| `LoadBalancingPolicyName` | `string?` | `null` | 策略名：`RoundRobin` / `PowerOfTwoChoices` / `LeastRequests` / `Random` / `WeightLoadBalancingPolicy`，为空即默认两次选择 |
| `GroupNamespaceMap` | `Dictionary<string, string>` | `{}` | 群组到 Namespace 的映射，未映射使用默认 Namespace |
| `InstanceMetadataFilter` | `Dictionary<string, string>` | `{}` | 实例元数据过滤，仅代理所有条件均匹配的实例 |

## 进阶用法

### 自定义路由映射规则

**方式一：实现接口（完全自定义）**

```csharp
public class MyConfigMapper : IYarpNacosPaoxyConfigMapper
{
    public RouteConfig CreateRouteConfig(string clusterId, string groupName, string serviceName)
    {
        return new RouteConfig
        {
            RouteId = $"{clusterId}-route",
            ClusterId = clusterId,
            Match = new RouteMatch
            {
                Path = $"/api/{serviceName}/{{**catch-all}}"
            },
            Transforms = new List<Dictionary<string, string>>
            {
                new() { { "PathRemovePrefix", $"/api/{serviceName}" } }
            }
        };
    }

    public ClusterConfig CreateClusterConfig(string clusterId,
        IReadOnlyDictionary<string, DestinationConfig> destinations)
    {
        return new ClusterConfig
        {
            ClusterId = clusterId,
            LoadBalancingPolicy = LoadBalancingPolicies.RoundRobin,
            Destinations = destinations
        };
    }

    public Dictionary<string, DestinationConfig> CreateDestinationConfig(List<Instance> instances)
    {
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

// 注册
builder.Services.AddYarpNacosPaoxy(builder =>
{
    builder.OptionBuilder(opt => { ... });
    builder.AddYarpNacosPaoxyConfigMapper<MyConfigMapper>();
});
```

**方式二：继承默认实现（微调部分逻辑）**

```csharp
public class MyMapper : YarpNacosPaoxyConfigMapper
{
    // 只覆盖路由规则，集群配置和实例映射沿用默认
    public override RouteConfig CreateRouteConfig(string clusterId, string groupName, string serviceName)
    {
        var config = base.CreateRouteConfig(clusterId, groupName, serviceName);
        config.Match.Path = $"/api/{serviceName}/{{**catch-all}}";
        return config;
    }
}
```

### 负载均衡策略

扩展内置了 5 种常用策略，一行代码即可切换：

```csharp
// YARP 内置策略（无需注册自定义实现）
builder.UsePowerOfTwoChoicesLoadBalancing();   // 两次选择（默认），兼顾随机与负载
builder.UseRoundRobinLoadBalancing();           // 轮询
builder.UseLeastRequestsLoadBalancing();        // 最少请求
builder.UseRandomLoadBalancing();               // 随机

// 自定义加权随机策略（需注册 WeightLoadBalancingPolicy）
builder.AddWeightLoadBalancingPolicy();         // 按 Nacos 实例权重分配
```

| 方法 | 策略 | 适用场景 |
|------|------|----------|
| `UsePowerOfTwoChoicesLoadBalancing()` | 两次选择 | 通用场景（默认） |
| `UseRoundRobinLoadBalancing()` | 轮询 | 实例性能相近 |
| `UseLeastRequestsLoadBalancing()` | 最少请求 | 长连接 / 耗时不均 |
| `UseRandomLoadBalancing()` | 随机 | 简单无状态 |
| `AddWeightLoadBalancingPolicy()` | 加权随机 | 实例性能差异大 |

同时也支持自定义策略：

```csharp
builder.AddLoadBalancingPolicy<MyPolicy>("my-policy");
```

### 多 Namespace 支持

不同群组分布在不同 Nacos Namespace 时：

```csharp
builder.Services.AddYarpNacosPaoxy(builder =>
{
    builder.OptionBuilder(opt =>
    {
        opt.GroupNameList = new() { "DEFAULT_GROUP", "API_GROUP" };
        opt.GroupNamespaceMap["API_GROUP"] = "namespace-api-xxx";
    });

    // 为其他 Namespace 注册对应的 INacosNamingService
    builder.AddNacosNamespaceClient("namespace-api-xxx",
        new NacosNamingService(new NacosNamingOptions
        {
            ServerAddresses = new() { "http://localhost:8848" },
            Namespace = "namespace-api-xxx",
            UserName = "user",
            Password = "pwd"
        }));
});
```

### 实例元数据过滤

仅代理满足特定元数据条件的实例：

```csharp
builder.OptionBuilder(opt =>
{
    opt.GroupNameList = new() { "DEFAULT_GROUP" };
    opt.InstanceMetadataFilter["env"] = "prod";
    opt.InstanceMetadataFilter["region"] = "cn";
});
```

### 服务上下线监听

实现 `IYarpNacosServiceChangeListener` 接口接收服务变更回调，用于告警、日志记录、指标上报等：

```csharp
public class MyServiceChangeListener : IYarpNacosServiceChangeListener
{
    public Task OnServiceOnlineAsync(string groupName, string serviceName, List<Instance> instances, CancellationToken ct)
    {
        Console.WriteLine($"服务上线: {groupName}/{serviceName}, 实例数: {instances.Count}");
        return Task.CompletedTask;
    }

    public Task OnServiceOfflineAsync(string groupName, string serviceName, CancellationToken ct)
    {
        Console.WriteLine($"服务下线: {groupName}/{serviceName}");
        // 发送告警通知
        return Task.CompletedTask;
    }

    public Task OnServiceChangedAsync(string groupName, string serviceName, List<Instance> instances, CancellationToken ct)
    {
        Console.WriteLine($"服务变更: {groupName}/{serviceName}, 最新实例数: {instances.Count}");
        return Task.CompletedTask;
    }
}

// 注册（支持多个监听器）
builder.AddServiceChangeListener<MyServiceChangeListener>();
```

### 自定义服务心跳

```csharp
builder.AddNacosServiceHeart<MyHeartbeatService>();

// MyHeartbeatService 继承 BackgroundService 实现自定义上下线检测逻辑
```

### 自定义日志输出

实现日志接口对接自己的日志系统（ELK、Splunk 等需要特定格式的场景）：

```csharp
public class MyLoggerFactory : IYarpNacosLoggerFactory
{
    public IYarpNacosLogger CreateLogger<T>() => new MyLogger(typeof(T).Name);
}

public class MyLogger : IYarpNacosLogger
{
    private readonly string _category;
    public MyLogger(string category) => _category = category;

    public void Info(string message)
        => Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [INFO] [{_category}] {message}");

    public void Warn(string message)
        => Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [WARN] [{_category}] {message}");

    public void Error(string message, Exception? ex = null)
        => Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [ERROR] [{_category}] {message} {ex?.Message}");
}

// 注册
builder.AddYarpNacosLoggerFactory<MyLoggerFactory>();
```

### 聚合 API 服务

网关接收客户端请求，内部通过 Nacos 发现并并发调用多个微服务，组装结果后返回：

**1. 定义 API 契约**

```csharp
public interface IOrderApi : IYarpNacosApiService
{
    Task<OrderDetail?> GetOrderAsync(string orderId);
}
```

**2. 实现聚合逻辑**

```csharp
public class OrderApiService : IOrderApi
{
    private readonly IYarpNacosHttpClient _client;

    public OrderApiService(IYarpNacosHttpClient client) => _client = client;

    public async Task<OrderDetail?> GetOrderAsync(string orderId)
    {
        // 并发调用多个 Nacos 微服务
        var order = await _client.GetAsync<Order>(opt =>
        {
            opt.ServiceName = "order-service";
            opt.Path = $"/api/orders/{orderId}";
            opt.TimeoutSeconds = 10;
            opt.RetryCount = 2;
        });

        var user = await _client.GetAsync<User>(opt =>
        {
            opt.ServiceName = "user-service";
            opt.Path = $"/api/users/{order.UserId}";
        });

        return new OrderDetail { Order = order, User = user };
    }
}
```

`IYarpNacosHttpClient` 由框架提供，自动完成 Nacos 服务发现 + HTTP 调用 + 随机负载均衡，支持两种调用方式：

```csharp
// 方式一：Action 配置模式
var order = await _client.GetAsync<Order>(opt =>
{
    opt.ServiceName = "order-service";
    opt.Path = $"/api/orders/{orderId}";
    opt.TimeoutSeconds = 10;
});

// 方式二：对象传入模式
var order = await _client.GetAsync<Order>(new NacosHttpRequest
{
    ServiceName = "order-service",
    Path = $"/api/orders/{orderId}",
    TimeoutSeconds = 10
}, cancellationToken);
```

**3. 注册**

```csharp
builder.Services.AddYarpNacosPaoxy(builder =>
{
    builder.OptionBuilder(opt => { ... });

    builder.AddAggregateApi<IOrderApi, OrderApiService>(opt =>
    {
        opt.BasePath = "/api/orders";
        opt.GroupName = "DEFAULT_GROUP";
        opt.TimeoutSeconds = 10;
        opt.RetryCount = 2;
    });
});
```

**4. 映射端点**

```csharp
app.MapGet("/api/orders/{id}", async (string id, IOrderApi api) =>
    await api.GetOrderAsync(id));
```

### 链路追踪接入

实现 `IYarpNacosTracing` 接口接入 OpenTelemetry、SkyWalking 或自定义追踪系统：

```csharp
// OpenTelemetry 示例
public class OpenTelemetryTracing : IYarpNacosTracing
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public OpenTelemetryTracing(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    public void EnrichRequest(HttpRequestMessage request)
    {
        // 将当前 Activity 的 traceparent 注入出站请求头
        var activity = Activity.Current;
        if (activity != null)
            request.Headers.TryAddWithoutValidation("traceparent", activity.Id);
    }

    public IDisposable? BeginSpan(string operationName, SpanKind kind, Dictionary<string, string>? tags)
        => null; // 依赖 ActivitySource 自动创建，此处可忽略
}

// 注册
builder.AddTracing<OpenTelemetryTracing>();
```

未注册时使用无操作默认实现，不影响正常功能。

## 可扩展组件

| 接口 | 用途 | 注册方法 |
|------|------|----------|
| `IYarpNacosPaoxyConfigMapper` | 路由/集群配置映射规则 | `AddYarpNacosPaoxyConfigMapper<T>()` |
| `IYarpNacosStore` | 状态管理（缓存、订阅） | `AddYarpNacosStore<T>()` |
| `IProxyConfigProvider` | Yarp 配置提供者 | `AddYarpProxyConfigProvider<T>()` |
| `IYarpNacosLoggerFactory` | 日志输出格式 | `AddYarpNacosLoggerFactory<T>()` |
| `IHostedService` | 服务心跳任务 | `AddNacosServiceHeart<T>()` |
| `ILoadBalancingPolicy` | 负载均衡策略 | `AddLoadBalancingPolicy<T>()` |
| `IYarpNacosServiceChangeListener` | 服务上下线回调 | `AddServiceChangeListener<T>()` |
| `IYarpNacosTracing` | 链路追踪（OpenTelemetry/SkyWalking） | `AddTracing<T>()` |
| `IYarpNacosApiService` | 聚合 API 契约 | `AddAggregateApi<TInterface, TImpl>()` |

## License

Apache-2.0
