using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace BTSS.IAR.Api.Models;

public sealed class TokenIssuer
{
    private readonly string _issuer;
    private readonly SecurityKey _key;

    public TokenIssuer(string issuer, SecurityKey key)
    {
        _issuer = issuer;
        _key = key;
    }

    public string IssueToken(IEnumerable<Claim> claims, int expiresMinutes)
    {
        var creds = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: null,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(expiresMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
