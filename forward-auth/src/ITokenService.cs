using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

public interface ITokenService
{
    string GenerateToken(string iss, string aud, ClaimsPrincipal user);
    bool ValidateToken(string iss, string aud, string token, [NotNullWhen(true)] out DateTime expiresOn);
}
