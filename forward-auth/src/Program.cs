
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Console;
using Microsoft.IdentityModel.Tokens;

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

builder.Services.AddAuthorization();
System.Threading.Thread.Sleep(0);
var githubOptions = builder.Configuration.GetSection("OAuth").Get<OAuthOptions>() ?? throw new Exception("There are no configured OAuth options configured.");
builder.Services.AddGithubAuth(githubOptions);
//builder.Services.AddAuthorization();
var app = builder.Build();
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto
});

app.Use(async (context, next) =>
{
    var forwardedHost = context.Request.Headers["X-Forwarded-Host"].ToString();
    if (!string.IsNullOrEmpty(forwardedHost))
    {
        var toplevel = GetTopLevelDomain(forwardedHost);
        var domain = "auth" + toplevel;

        var isHttps = context.Request.Headers["X-Forwarded-Proto"] == "https";
        context.Request.Host = new HostString(domain);

        if (isHttps)
            context.Request.IsHttps = true;
    }

    await next(context);
});

app.Map("/auth", async (HttpContext context) =>
{
    if (context.User?.Identity?.IsAuthenticated != true)
    {
        var proto = context.Request.IsHttps ? "https" : "http";
        var fullUrl = Base64UrlEncoder.Encode(proto + "://" + context.Request.Headers["X-Forwarded-Host"] + context.Request.Headers["X-Forwarded-Uri"]);
        await context.ChallengeAsync("Github", new AuthenticationProperties()
        {
            RedirectUri = $"https://{githubOptions.Domain}/authed?returnUrl=" + fullUrl
        });
    }
});

app.Map("/verify", async (HttpContext context) =>
{
    if (context.User?.Identity?.IsAuthenticated == true)
        await context.Response.WriteAsync("OK");
});



app.UseAuthentication();
// app.Use(async (context, next) =>
// {
//     var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
//     // foreach (var it in context.Request.Headers)
//     // {
//     //     logger.LogInformation($"header: {it.Key}, value: {it.Value}");
//     // }
//     logger.LogInformation($"user = {context.User?.Identity?.IsAuthenticated}");

//     await next(context);
// });
app.UseAuthorization();
app.Map("/authed", async (HttpContext context, [FromQuery] string returnUrl) =>
{
    var fullUrl = Base64UrlEncoder.Decode(returnUrl);
    context.Response.Redirect(fullUrl);
    await Task.CompletedTask;
}).RequireAuthorization();


// app.MapFallback((HttpContext context) =>
// {
//     var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

//     var fullUrl = context.Request.Scheme + "://" + context.Request.Host + context.Request.Path + context.Request.QueryString;
//     logger.LogInformation($"FALLBACK!!!!!!!!!! URL: {fullUrl}");
// });

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
}
