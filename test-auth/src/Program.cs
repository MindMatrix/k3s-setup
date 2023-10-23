
using Microsoft.Extensions.Logging.Console;

var builder = WebApplication.CreateBuilder(args);
// Add services to the container.

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

var app = builder.Build();

app.MapFallback(async (HttpContext context) =>
{
    foreach (var it in context.Request.Headers)
        await context.Response.WriteAsync($"header: {it.Key}, value: {it.Value}\n");
});

app.Run();

