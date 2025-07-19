using Lycoris.Yarp.Nacos.Extensions;
using Nacos.V2.DependencyInjection;
using YarpNacosSample2;

var builder = WebApplication.CreateBuilder(args);

// 添加Nacos远端配置中心服务
builder.Services.AddNacosV2Config(opt =>
{
    opt.EndPoint = string.Empty;
    opt.ServerAddresses = ["your nacos service ipaddress"];
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
    opt.ServerAddresses = ["your nacos service ipaddress"];
    opt.Namespace = "your service namespace";

    opt.UserName = "your username";
    opt.Password = "your password";

    // swich to use http or rpc
    opt.NamingUseRpc = true;
});

// 添加Yarp服务及Nacos注册中心扩展
builder.Services.AddYarpNacosPaoxy(builder =>
{
    builder.OptionBuilder(opt =>
    {
        opt.GroupNameList = ["your service groupname"];
    });

    builder.AddYarpNacosPaoxyConfigMapper<CustomePaoxyConfigMapper>();
    builder.AddYarpProxyConfigProvider<CustomeConfigProvider>();
});

// Add services to the container.

var app = builder.Build();

// Configure the HTTP request pipeline.

// 
app.UseRouting();

// 
app.MapReverseProxy(proxyPipeline =>
{
    proxyPipeline.UsePassiveHealthChecks();
});

app.Run();
