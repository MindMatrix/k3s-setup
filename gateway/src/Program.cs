using System.Net.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Console;
using Microsoft.IdentityModel.Tokens;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);
// Add services to the container.

builder.WebHost.ConfigureKestrel((context, options) =>
{
    options.AddServerHeader = false;
    options.ListenAnyIP(4111, endpoint =>
    {
        //endpoint.UseHttps();
    });
});

builder.Logging.AddSimpleConsole(options =>
{
    options.ColorBehavior = LoggerColorBehavior.Enabled;
});

var githubOptions = builder.Configuration.GetSection("OAuth").Get<OAuthOptions>() ?? throw new Exception("There are no configured OAuth options configured.");
builder.Services.AddGithubAuth(githubOptions);
builder.Services.AddAuthorization();


builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(builderContext =>
    {
        builderContext.AddRequestTransform(transformContext =>
        {
            if (transformContext.HttpContext.User?.Identity?.IsAuthenticated == true)
                transformContext.ProxyRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test");

            return ValueTask.CompletedTask;
        });
        builderContext.AddResponseTransform(async transformContext =>
        {
            transformContext.ProxyResponse?.Headers.Remove("Server");
            transformContext.HttpContext.Response.Headers.Remove("Server");

            if (transformContext.ProxyResponse?.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                var customAuth = transformContext.ProxyResponse?.Headers.GetValues("X-Custom-Auth").FirstOrDefault();
                if (!string.IsNullOrEmpty(customAuth))
                {
                    transformContext.SuppressResponseBody = true;
                    transformContext.ProxyResponse?.Headers.Remove("X-Custom-Auth");
                    if (transformContext.HeadersCopied)
                        transformContext.HttpContext.Response.Headers.Remove("X-Custom-Auth");

                    if (customAuth == "API")
                    {
                        transformContext.HttpContext.Response.StatusCode = 401;
                    }
                    else
                    {
                        await ChallengeGithub(transformContext.HttpContext, githubOptions);
                    }
                }
            }
            //return ValueTask.CompletedTask;
        });
    })
    .ConfigureHttpClient((context, handler) =>
    {
        handler.SslOptions.RemoteCertificateValidationCallback = (_, __, ___, ____) => true;
    });

var app = builder.Build();
// app.UseForwardedHeaders(new ForwardedHeadersOptions
// {
//     ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost
// });

app.Use(async (context, next) =>
{
    var forwardedHost = context.Request.Headers["X-Forwarded-Host"].ToString();
    if (!string.IsNullOrEmpty(forwardedHost))
    {
        var isHttps = context.Request.Headers["X-Forwarded-Proto"] == "https";
        context.Request.Host = new HostString(forwardedHost);

        if (isHttps)
            context.Request.IsHttps = true;
    }

    await next(context);
});

// app.Map("/auth", async (HttpContext context) =>
// {
//     if (context.User?.Identity?.IsAuthenticated != true)
//     {
//         var proto = context.Request.IsHttps ? "https" : "http";
//         var fullUrl = Base64UrlEncoder.Encode(proto + "://" + context.Request.Headers["X-Forwarded-Host"] + context.Request.Headers["X-Forwarded-Uri"]);
//         await context.ChallengeAsync("Github", new AuthenticationProperties()
//         {
//             RedirectUri = $"https://{githubOptions.Domain}/authed?returnUrl=" + fullUrl
//         });
//     }
// });

app.UseAuthentication();
//app.MapReverseProxy();
app.MapReverseProxy(proxyPipeline =>
{
    proxyPipeline.Use(async (context, next) =>
    {
        if (context.Request.Host.Host == githubOptions.Domain)
            return;

        if (context.Request.Headers.ContainsKey("X-Github-Auth") && context.User?.Identity?.IsAuthenticated != true)
        {
            await ChallengeGithub(context, githubOptions);
            return;
        }
        await next();
    });
});


app.UseAuthorization();
// app.Map("/authed", async (HttpContext context, [FromQuery] string returnUrl) =>
// {
//     var fullUrl = Base64UrlEncoder.Decode(returnUrl);
//     context.Response.Redirect(fullUrl);
//     await Task.CompletedTask;
// }).RequireAuthorization();


// app.MapFallback((HttpContext context) =>
// {
//     var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

//     var fullUrl = context.Request.Scheme + "://" + context.Request.Host + context.Request.Path + context.Request.QueryString;
//     logger.LogInformation($"FALLBACK!!!!!!!!!! URL: {fullUrl}");
// });
app.Map("/authed", async (HttpContext context, [FromQuery] string returnUrl) =>
{
    var fullUrl = Base64UrlEncoder.Decode(returnUrl);
    context.Response.Redirect(fullUrl);
    await Task.CompletedTask;
}).RequireAuthorization();

app.Run();

public partial class Program
{
    public static string GetTopLevelDomain(string host)
    {
        var count = host.Count(x => x == '.');
        if (count <= 1)
            return host;

        var dotIndex = host.IndexOf('.');
        return host.Substring(dotIndex);
    }

    static async Task ChallengeGithub(HttpContext context, OAuthOptions githubOptions)
    {
        var request = context.Request;
        var host = request.Host.ToUriComponent();
        var pathBase = request.PathBase.ToUriComponent();
        var path = request.Path.ToUriComponent();
        var queryString = request.QueryString.ToUriComponent();

        string fullUrl = Base64UrlEncoder.Encode($"{request.Scheme}://{host}{pathBase}{path}{queryString}");
        await context.ChallengeAsync("Github", new AuthenticationProperties()
        {
            RedirectUri = $"https://{githubOptions.Domain}/authed?returnUrl=" + fullUrl
        });
    }
}

