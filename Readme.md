## **yarp网关使用nacos做为注册中心，监控服务上下线并自动生成配置文件**

**安装：`dotnet add package Lycoris.Yarp.Nacos.Extensions`**

**扩展默认的路由匹配规则为：**

**`http://ip:port/nacos服务群组/nacos微服务名称/后续接口地址`**

**转换为**

**`http://微服务实例IP:微服务实例端口/后续接口地址`**

**使用方法：**

```csharp
var builder = WebApplication.CreateBuilder(args);

// 添加Nacos相关服务
builder.Services.AddNacosV2Config(opt =>
{
    opt.EndPoint = string.Empty;
    opt.ServerAddresses = new List<string>() { "your nacos service ipaddress" };
    opt.Namespace = "your service namespace";

    opt.UserName = "your username";
    opt.Password = "your password";

    // swich to use http or rpc
    opt.ConfigUseRpc = true;
});

// 添加Nacos相关服务
builder.Services.AddNacosV2Naming(opt =>
{
    opt.EndPoint = string.Empty;
    opt.ServerAddresses = new List<string>() { "your nacos service ipaddress" };
    opt.Namespace = "your service namespace";

    opt.UserName = "your username";
    opt.Password = "your password";

    // swich to use http or rpc
    opt.NamingUseRpc = true;
});

// 添加扩展
builder.Services.AddYarpNacosPaoxy(builder =>
{
    builder.OptionBuilder(opt =>
    {
        // 需要生成配置的Nacos上的群组名称列表
        // 扩展会拉取这些群组下的所有微服务集群生成反向代理配置
         opt.GroupNameList = new List<string>() { "your service groupname" };
    });
});

// 添加Yarp服务
builder.Services.AddReverseProxy();

// Add services to the container.

var app = builder.Build();

// Configure the HTTP request pipeline.

// 
app.UseRouting();

// 
app.UseEndpoints(endpoints =>
{
    endpoints.MapReverseProxy(proxyPipeline =>
    {
        proxyPipeline.UsePassiveHealthChecks();
    });
});

app.Run();
```

**当然扩展也支持自定义规则，只需要实现`IYarpNacosPaoxyConfigMapper`接口。**

```csharp
builder.Services.AddYarpNacosPaoxy(builder =>
{
    builder.OptionBuilder(opt =>
    {
        // 需要生成配置的Nacos上的群组名称列表
         opt.GroupNameList = new List<string>() { "your service groupname" };
    });

    builder.AddYarpNacosPaoxyConfigMapper<CustomePaoxyConfigMapper>();
});
```

**当然你还想自定义更多，扩展也支持你自己改造，具体的可自定义的接口如下**

- **`IYarpNacosPaoxyConfigMapper`：网关反向代理规则。**
- **`IYarpNacosStore`：规则管理(包含获取规则、热重载、实时更新规则)。**
- **`IProxyConfigProvider`：Yarp代理的接口，用来重载反向代理配置。**
- **`ILoggerFactory`：扩展的自定义日志工厂，在开发的时候，使用别人的扩展，但是由于自己的日志需要按格式，ES才能进行切割关键词分片等，很多不支持，所以自己开发的时候额外增加了这部分。**

**注意：使用自定义日志工厂时候，还需要自己实现`ILycorisLogger`来配合日志工厂实现自定义日志功能**

```csharp
builder.Services.AddYarpNacosPaoxy(builder =>
{
    builder.OptionBuilder(opt =>
    {
        // 需要生成配置的Nacos上的群组名称列表
         opt.GroupNameList = new List<string>() { "your service groupname" };
    });

    builder.AddYarpNacosPaoxyConfigMapper<CustomePaoxyConfigMapper>();
    builder.AddYarpNacosStore<CustomeStore>();
    builder.AddYarpProxyConfigProvider<CustomeConfigProvider>();
    builder.AddLycorisLoggerFactory<CustomeLoggerFactory>();
});
```

**PS:如果你使用了多个Lycoris系列扩展,那你可以在注册这些扩展之前使用`builder.Serovces.AddLycorisLoggerFactory<CustomeLoggerFactory>()`进行替换，就不需要在每个扩展中使用`AddLycorisLoggerFactory`进行逐一替换了**


**另外，还有Nacos的心跳监听服务，用来实时观测是否用新的集群服务上下线，并自动更新反向代理配置，当然它也是支持自定义的，只需要实现.Net的`IHostedService`**

```csharp
builder.Services.AddYarpNacosPaoxy(builder =>
{
    builder.OptionBuilder(opt =>
    {
        // 需要生成配置的Nacos上的群组名称列表
         opt.GroupNameList = new List<string>() { "your service groupname" };
    });

    builder.AddNacosServiceHeart<CustomeServiceHeart>();
});
```

