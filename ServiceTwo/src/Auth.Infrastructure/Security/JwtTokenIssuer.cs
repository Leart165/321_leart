using Auth.Application.Abstractions;
using Auth.Domain.Users;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Auth.Infrastructure.Security;

public sealed class JwtTokenIssuer : ITokenIssuer
{
    private readonly RsaSigningKeyStore _signingKeys;
    private readonly JwtOptions _options;
    private readonly IClock _clock;

    public JwtTokenIssuer(RsaSigningKeyStore signingKeys, IOptions<JwtOptions> options, IClock clock)
    {
        _signingKeys = signingKeys;
        _options = options.Value;
        _clock = clock;
    }

    public IssuedToken Issue(User user)
    {
        DateTime now = _clock.Now.UtcDateTime;
        DateTime expires = now.AddSeconds(_options.AccessTokenLifetimeSeconds);

        RsaSecurityKey signingKey = new RsaSecurityKey(_signingKeys.Rsa) { KeyId = _signingKeys.KeyId };
        SigningCredentials credentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256);

        List<Claim> claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Email, user.Email.Value)
        };
        if (!string.IsNullOrWhiteSpace(user.DisplayName))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Name, user.DisplayName));
        }

        JwtSecurityToken token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: credentials);

        token.Payload[JwtRegisteredClaimNames.Sub] = user.Id.ToString();

        string accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        return new IssuedToken(accessToken, _options.AccessTokenLifetimeSeconds);
    }
}
