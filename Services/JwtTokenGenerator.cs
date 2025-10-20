using System.Security.Claims;
using System.Text;
using Cap1.LogiTrack.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace Cap1.LogiTrack.Services;

public class JwtTokenGenerator
{
    private readonly JwtOption _jwtOption;
    public JwtTokenGenerator(IOptions<JwtOption> jwtOption)
    {
        _jwtOption = jwtOption.Value ?? throw new ArgumentNullException(nameof(jwtOption));
    }
    public string GenerateToken(ApplicationUser user)
    {
        var secret = _jwtOption.Key ?? throw new InvalidOperationException("JWT Key is not configured.");
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim("sub", user.Id),
            new Claim("email", user.Email ?? string.Empty),
            new Claim("jti", Guid.NewGuid().ToString())
        };
        var token = new JwtSecurityToken(
            issuer: _jwtOption.Issuer,
            audience: _jwtOption.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtOption.ExpireMinutes),
            signingCredentials: credentials
        );
        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(token);
    }
}