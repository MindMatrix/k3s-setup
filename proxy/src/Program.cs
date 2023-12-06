
using System.Text;
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
            //builderContext.AddForwarded();
            builderContext.AddOriginalHost();
            // builderContext.AddRequestTransform(transformContext =>
            // {
            //     transformContext.ProxyRequest.Headers.Host = transformContext.HttpContext.Request.Host.Host; // FAILS WITH OUT

            //     return ValueTask.CompletedTask;
            // });
        });

var app = builder.Build();

// app.Use((context, next) =>
// {
//     context.Request.Host = new HostString(context.Request.Host.Host, 82); // FAILS WITH OUT
//     return next();
// });

app.UseRouting();
//app.UseMiddleware<RequestResponseLoggingMiddleware>();

app.MapReverseProxy(proxyPipeline =>
{
    proxyPipeline.UseLoadBalancing();
});
//curl localhost:4181/mm.amp.vg/login

app.Run();

public class RequestResponseLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestResponseLoggingMiddleware> _logger;

    public RequestResponseLoggingMiddleware(RequestDelegate next, ILogger<RequestResponseLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Log Request
        var sb = new StringBuilder();
        sb.AppendLine("Request Information:");
        sb.AppendLine($"Schema:{context.Request.Scheme}");
        sb.AppendLine($"Host: {context.Request.Host}");
        sb.AppendLine($"Path: {context.Request.Path}");
        sb.AppendLine($"QueryString: {context.Request.QueryString}");
        sb.AppendLine($"Request Body: {await GetRequestBody(context.Request)}");
        foreach (var header in context.Request.Headers)
        {
            sb.AppendLine($"Header: {header.Key}: {header.Value}");
        }

        // Copy a pointer to the original response body stream
        var originalResponseBodyStream = context.Response.Body;

        // Create a new memory stream...
        using (var responseBody = new MemoryStream())
        {
            // ...and use that for the temporary response body
            context.Response.Body = responseBody;

            // Continue down the Middleware pipeline, eventually returning here
            await _next(context);

            // Format the response from the server
            sb.AppendLine($"Response Information:");
            sb.AppendLine($"Status Code: {context.Response.StatusCode}");
            sb.AppendLine($"Response Body: {await GetResponseBody(responseBody)}");
            foreach (var header in context.Response.Headers)
            {
                sb.AppendLine($"Header: {header.Key}: {header.Value}");
            }

            // Copy the contents of the new memory stream (which contains the response) to the original stream, which is then returned to the client.
            responseBody.Seek(0, SeekOrigin.Begin);
            await responseBody.CopyToAsync(originalResponseBodyStream);
        }
        _logger.LogInformation(sb.ToString());
    }

    private async Task<string> GetRequestBody(HttpRequest request)
    {
        request.EnableBuffering();
        var body = request.Body;
        var buffer = new byte[Convert.ToInt32(request.ContentLength)];
        await request.Body.ReadAsync(buffer, 0, buffer.Length);
        string requestBody = Encoding.UTF8.GetString(buffer);
        body.Seek(0, SeekOrigin.Begin);
        request.Body = body;
        return requestBody;
    }

    private async Task<string> GetResponseBody(MemoryStream responseBody)
    {
        responseBody.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(responseBody).ReadToEndAsync();
        responseBody.Seek(0, SeekOrigin.Begin);
        return body;
    }
}
