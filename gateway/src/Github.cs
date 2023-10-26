using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.IdentityModel.Tokens;

public static class GitHub
{
    public static void AddGithubAuth(this IServiceCollection services, OAuthOptions githubOptions)
    {
        var toplevel = Program.GetTopLevelDomain(githubOptions.Domain);

        services
            .AddAuthentication(opt =>
            {
                opt.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                opt.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                opt.DefaultChallengeScheme = "Github";
            })
            .AddCookie(options =>
            {
                var securityKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(githubOptions.JwtKey));
                var creds = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = creds.Key,
                    ValidateIssuer = true,
                    ValidIssuer = githubOptions.Domain,
                    ValidateAudience = true,
                    ValidAudience = toplevel,
                    ValidateLifetime = true, // When false, the token never expires. Useful for debugging
                };

                options.TicketDataFormat = new JwtTicketDataFormat(
                    24,
                    validationParameters,
                    creds
                );

                options.Cookie.Domain = toplevel;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.ExpireTimeSpan = TimeSpan.FromHours(24);
                options.SlidingExpiration = false;
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
                options.Cookie.SameSite = SameSiteMode.Strict;
            })
            .AddGitHub("Github", options =>
            {
                options.CorrelationCookie.Domain = toplevel;
                options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.CorrelationCookie.HttpOnly = true;
                options.CorrelationCookie.IsEssential = true;
                options.CorrelationCookie.SameSite = SameSiteMode.Strict;
                options.Events = new OAuthEvents
                {
                    OnRedirectToAuthorizationEndpoint = context =>
                    {
                        // Extract the original redirect_uri from the query string
                        var query = System.Web.HttpUtility.ParseQueryString(new Uri(context.RedirectUri).Query);
                        var originalRedirectUri = query["redirect_uri"];

                        if (originalRedirectUri != null)
                        {
                            query.Remove("redirect_uri");
                            var updatedOAuthUri = new UriBuilder(context.RedirectUri)
                            {
                                Query = query.ToString()
                            };

                            context.RedirectUri = updatedOAuthUri.ToString();
                        }

                        // Continue with the redirect logic
                        context.Response.Redirect(context.RedirectUri);

                        return Task.CompletedTask;
                    },
                    OnCreatingTicket = async ticketContext =>
                    {
                        var logger = ticketContext.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                        var userString = await CallGithubEndpoint(ticketContext.Options.UserInformationEndpoint);

                        var userDocument = JsonDocument.Parse(userString);
                        var githubTicket = JsonSerializer.Deserialize<GithubTicket>(userString);

                        if (githubTicket == null)
                        {
                            logger.LogError("Failed to get github ticket.");
                            ticketContext.Fail(new Exception("Failed to get github ticket."));
                            return;
                        }

                        var username = githubTicket.login;

                        var orgMembershipString = await CallGithubEndpoint($"https://api.github.com/orgs/MindMatrix/memberships/{username}");
                        var orgMembership = JsonSerializer.Deserialize<GithubOrgMembership>(orgMembershipString);

                        if (orgMembership == null)
                        {
                            logger.LogError("You are not part of the mindmatrix org.");
                            ticketContext.Fail(new Exception("You are not part of the mindmatrix org."));
                            return;
                        }

                        async Task<string> CallGithubEndpoint(string requestUrl)
                        {
                            var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ticketContext.AccessToken);
                            var response = await ticketContext.Backchannel.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ticketContext.HttpContext.RequestAborted);
                            response.EnsureSuccessStatusCode();
                            return await response.Content.ReadAsStringAsync();
                        }
                        ticketContext.RunClaimActions(userDocument.RootElement);

                        if (ticketContext.Identity == null)
                            throw new UnauthorizedAccessException();

                        if (string.IsNullOrWhiteSpace(githubTicket.email))
                        {
                            var emailJson = await CallGithubEndpoint("https://api.github.com/user/emails");
                            var emails = JsonSerializer.Deserialize<GithubEmail[]>(emailJson);

                            if (emails == null || emails.Length == 0 || !emails.Any(x => x.primary) || !emails.Any(x => x.verified))
                            {
                                logger.LogError("You must provide a valid and verified email to github thats marked primary.");
                                ticketContext.Fail(new Exception("You must provide a valid and verified email to github thats marked primary."));
                                return;
                            }

                            githubTicket.email = emails.First(x => x.primary).email;
                        }

                        var teamsJson = await CallGithubEndpoint("https://api.github.com/orgs/MindMatrix/teams");
                        var githubTeams = JsonSerializer.Deserialize<GithubTeam[]>(teamsJson);
                        var teams = githubTeams?.Select(x => new Claim(ClaimTypes.Role, x.slug)).ToArray() ?? new Claim[0];
                        ticketContext.Identity.AddClaims(teams);
                        ticketContext.Identity.AddClaim(new Claim(ClaimTypes.Email, githubTicket.email!));
                        ticketContext.Identity.AddClaim(new Claim("location", ticketContext.HttpContext.Request.Headers["X-Forwarded-Uri"].ToString()));

                        logger.LogInformation("Successfully auth via github and set claims.");
                    }
                };

                options.Scope.Add("read:user");
                options.Scope.Add("user:email");
                options.Scope.Add("read:org");
                options.ClientId = githubOptions.ClientID;
                options.ClientSecret = githubOptions.ClientSecret;
            });
    }
}
