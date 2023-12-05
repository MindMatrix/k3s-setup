
using Microsoft.Extensions.Logging.Console;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);
// Add services to the container.
// if (Environment.GetEnvironmentVariable("KUBERNETES_ENVIRONMENT") == "true")
if (builder.Environment.IsProduction())
    builder.Configuration.AddJsonFile("/app/config/appsettings.json", optional: false, reloadOnChange: true);

builder.WebHost.ConfigureKestrel((context, options) =>
{
    options.ListenAnyIP(4181, endpoint =>
    {
        //endpoint.UseHttps();
    });
});

builder.Logging.AddSimpleConsole(options =>
{
    options.ColorBehavior = LoggerColorBehavior.Enabled;
});

builder.Services.AddReverseProxy()
        .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
        .AddTransforms(builderContext =>
        {
            builderContext.AddRequestTransform(transformContext =>
            {
                var path = transformContext.HttpContext.Request.Path.Value;
                transformContext.ProxyRequest.Headers.Host = transformContext.HttpContext.Request.Host.Host;

                return ValueTask.CompletedTask;
            });
        });

var app = builder.Build();

app.Use((context, next) =>
    {
        var path = context.Request.Path.Value;
        var segments = path?.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments?.Length > 0)
        {
            var oldHost = context.Request.Host;
            context.Request.Host = new HostString(segments[0], 80);

            // Adjust the path
            var newPath = "/" + string.Join("/", segments.Skip(1));
            context.Request.Path = newPath;
        }

        return next();
    });

app.UseRouting();

app.MapReverseProxy(proxyPipeline =>
{
    proxyPipeline.UseLoadBalancing();
});
//curl localhost:4181/mm.amp.vg/login

app.Run();

