using Lycoris.Yarp.Nacos.Extensions;
using Nacos.V2.DependencyInjection;
using YarpNacosSample2;

var builder = WebApplication.CreateBuilder(args);

// ����NacosԶ���������ķ���
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

// ����Nacos��ط���
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

// ����Yarp����Nacosע��������չ
builder.Services.AddYarpNacosPaoxy(builder =>
{
    builder.OptionBuilder(opt =>
    {
        opt.GroupNameList = new List<string>() { "your service groupname" };
    });

    builder.AddYarpNacosPaoxyConfigMapper<CustomePaoxyConfigMapper>();
    builder.AddYarpProxyConfigProvider<CustomeConfigProvider>();
});

// Add services to the container.

var app = builder.Build();

// Configure the HTTP request pipeline.

app.MapReverseProxy(proxyPipeline =>
{
    proxyPipeline.UsePassiveHealthChecks();
});

app.Run();
