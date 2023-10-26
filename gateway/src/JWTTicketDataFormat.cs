using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

public class JwtTicketDataFormat : ISecureDataFormat<AuthenticationTicket>
{
    private readonly JwtSecurityTokenHandler _tokenHandler;
    private readonly TokenValidationParameters _validationParameters;
    private readonly SigningCredentials _signingCredentials;
    private readonly int _duration = 24;

    public JwtTicketDataFormat(int duration,
                               TokenValidationParameters validationParameters,
                               SigningCredentials signingCredentials)
    {
        _duration = duration;
        _tokenHandler = new JwtSecurityTokenHandler();
        _validationParameters = validationParameters;
        _signingCredentials = signingCredentials;
    }

    public string Protect(AuthenticationTicket data)
    {
        return Protect(data, null);
    }

    public string Protect(AuthenticationTicket data, string? purpose)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        var token = new JwtSecurityToken(
            issuer: _validationParameters.ValidIssuer,
            audience: _validationParameters.ValidAudience,
            claims: data.Principal.Claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: _signingCredentials
        );

        return _tokenHandler.WriteToken(token);
    }

    public AuthenticationTicket? Unprotect(string? protectedText)
    {
        return Unprotect(protectedText, null);
    }

    public AuthenticationTicket? Unprotect(string? protectedText, string? purpose)
    {
        try
        {
            var principal = _tokenHandler.ValidateToken(protectedText, _validationParameters, out var validToken);
            if (validToken.ValidTo.ToUniversalTime() > DateTime.UtcNow)
                return new AuthenticationTicket(principal, new AuthenticationProperties(), "Cookie");
        }
        catch
        {

        }
        return null;
    }
}
