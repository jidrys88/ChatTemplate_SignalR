using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ChatTemplate.Core.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace ChatTemplate.API.Controllers;

/// <summary>
/// Test-Controller zur Ausstellung von JWTs fuer die Entwicklung/Demo-Zwecke.
/// In einer echten Anwendung wuerde hier eine vollstaendige Identitaetspruefung
/// (Passwort-Hash-Vergleich, externer Identity Provider, etc.) stattfinden - dieser
/// Endpoint akzeptiert bewusst nur eine UserId/DisplayName, um das Template einfach
/// end-to-end testbar zu machen.
/// </summary>
/// <remarks>C# 13 Primary Constructor fuer die injizierte Konfiguration.</remarks>
[ApiController]
[Route("api/[controller]")]
public sealed class AuthController(IConfiguration configuration) : ControllerBase
{
    /// <summary>
    /// Stellt ein JWT fuer die angegebene User-ID/den Anzeigenamen aus.
    /// POST /api/auth/token
    /// </summary>
    [HttpPost("token")]
    [ProducesResponseType<LoginResponseDto>(StatusCodes.Status200OK)]
    public ActionResult<LoginResponseDto> IssueToken([FromBody] LoginRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            return BadRequest("UserId darf nicht leer sein.");
        }

        var jwtSection = configuration.GetSection("JwtSettings");
        var secretKey = jwtSection["SecretKey"]
            ?? throw new InvalidOperationException("JwtSettings:SecretKey ist nicht konfiguriert.");
        var issuer = jwtSection["Issuer"];
        var audience = jwtSection["Audience"];
        var expiryMinutes = jwtSection.GetValue("ExpiryMinutes", 60);

        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, request.UserId),
            new(ClaimTypes.Name, request.DisplayName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        ];

        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            SecurityAlgorithms.HmacSha256);

        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(expiryMinutes);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: signingCredentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        return Ok(new LoginResponseDto(accessToken, expiresAt));
    }
}
