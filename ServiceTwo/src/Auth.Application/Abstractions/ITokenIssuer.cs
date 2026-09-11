using Auth.Domain.Users;

namespace Auth.Application.Abstractions;

public interface ITokenIssuer
{
    IssuedToken Issue(User user);
}

public sealed record IssuedToken(string AccessToken, int ExpiresInSeconds);
