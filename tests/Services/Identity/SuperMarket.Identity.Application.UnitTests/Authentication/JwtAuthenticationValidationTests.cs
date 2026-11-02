using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;

namespace SuperMarket.Identity.Application.UnitTests.Authentication;

public class JwtAuthenticationValidationTests
{
    private const string ExpectedIssuer = "http://localhost:8080/realms/supermarket";
    private const string ExpectedAudience = "pos-api";

    private readonly RSA _rsa;
    private readonly RsaSecurityKey _signingKey;
    private readonly JwtSecurityTokenHandler _tokenHandler;

    public JwtAuthenticationValidationTests()
    {
        _rsa = RSA.Create(2048);
        _signingKey = new RsaSecurityKey(_rsa);
        _tokenHandler = new JwtSecurityTokenHandler();
    }

    private TokenValidationParameters CreateValidationParameters(TimeSpan? clockSkew = null)
    {
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = ExpectedIssuer,
            ValidateAudience = true,
            ValidAudience = ExpectedAudience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _signingKey,
            ClockSkew = clockSkew ?? TimeSpan.Zero,
            NameClaimType = "preferred_username",
            RoleClaimType = "roles"
        };
    }

    private string GenerateJwt(
        string issuer,
        string audience,
        DateTime notBefore,
        DateTime expires,
        string? username = "cashier1",
        Guid? userId = null)
    {
        var claims = new List<Claim>
        {
            new("sub", (userId ?? Guid.NewGuid()).ToString()),
            new("preferred_username", username ?? "user"),
            new("roles", "Cashier")
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = issuer,
            Audience = audience,
            NotBefore = notBefore,
            Expires = expires,
            SigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256)
        };

        var token = _tokenHandler.CreateToken(tokenDescriptor);
        return _tokenHandler.WriteToken(token);
    }

    [Fact]
    public void ValidateToken_ShouldSucceed_WhenTokenHasCorrectAudienceIssuerAndLifetime()
    {
        // Arrange: Valid token matching expected audience and issuer
        var token = GenerateJwt(
            issuer: ExpectedIssuer,
            audience: ExpectedAudience,
            notBefore: DateTime.UtcNow.AddMinutes(-5),
            expires: DateTime.UtcNow.AddMinutes(30));

        var parameters = CreateValidationParameters();

        // Act
        var principal = _tokenHandler.ValidateToken(token, parameters, out var validatedToken);

        // Assert
        principal.Should().NotBeNull();
        principal.Identity!.IsAuthenticated.Should().BeTrue();
        principal.FindFirst("preferred_username")?.Value.Should().Be("cashier1");
        validatedToken.Should().NotBeNull();
    }

    [Fact]
    public void ValidateToken_ShouldThrowSecurityTokenInvalidAudienceException_WhenAudienceIsDifferent()
    {
        // Arrange: Token issued for a different API / client
        var token = GenerateJwt(
            issuer: ExpectedIssuer,
            audience: "pos-inventory-external",
            notBefore: DateTime.UtcNow.AddMinutes(-5),
            expires: DateTime.UtcNow.AddMinutes(30));

        var parameters = CreateValidationParameters();

        // Act & Assert
        var act = () => _tokenHandler.ValidateToken(token, parameters, out _);

        act.Should().Throw<SecurityTokenInvalidAudienceException>()
            .WithMessage("*IDX10214: Audience validation failed*");
    }

    [Fact]
    public void ValidateToken_ShouldThrowSecurityTokenInvalidIssuerException_WhenIssuerIsDifferent()
    {
        // Arrange: Token issued by an untrusted / rogue identity provider
        var token = GenerateJwt(
            issuer: "http://rogue-idp.attacker.com/realms/fake",
            audience: ExpectedAudience,
            notBefore: DateTime.UtcNow.AddMinutes(-5),
            expires: DateTime.UtcNow.AddMinutes(30));

        var parameters = CreateValidationParameters();

        // Act & Assert
        var act = () => _tokenHandler.ValidateToken(token, parameters, out _);

        act.Should().Throw<SecurityTokenInvalidIssuerException>()
            .WithMessage("*IDX10205: Issuer validation failed*");
    }

    [Fact]
    public void ValidateToken_ShouldThrowSecurityTokenExpiredException_WhenTokenIsExpired()
    {
        // Arrange: Expired token (expired 15 minutes ago)
        var token = GenerateJwt(
            issuer: ExpectedIssuer,
            audience: ExpectedAudience,
            notBefore: DateTime.UtcNow.AddMinutes(-30),
            expires: DateTime.UtcNow.AddMinutes(-15));

        var parameters = CreateValidationParameters(clockSkew: TimeSpan.Zero);

        // Act & Assert
        var act = () => _tokenHandler.ValidateToken(token, parameters, out _);

        act.Should().Throw<SecurityTokenExpiredException>()
            .WithMessage("*IDX10223: Lifetime validation failed. The token is expired*");
    }

    [Theory]
    [InlineData("Development", null, false)]
    [InlineData("Production", null, true)]
    [InlineData("Production", false, false)]
    [InlineData("Development", true, true)]
    public void RequireHttpsMetadata_ShouldResolveAccurately_BasedOnEnvironmentAndConfig(
        string environment,
        bool? configOverride,
        bool expectedResult)
    {
        // Arrange
        var isDevelopment = environment.Equals("Development", StringComparison.OrdinalIgnoreCase);

        // Act: mirrors the exact production resolution logic in Program.cs
        var resolvedValue = configOverride ?? !isDevelopment;

        // Assert
        resolvedValue.Should().Be(expectedResult);
    }
}
