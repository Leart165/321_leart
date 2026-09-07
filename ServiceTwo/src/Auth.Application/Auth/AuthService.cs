using Auth.Application.Abstractions;
using Auth.Domain.Exceptions;
using Auth.Domain.Users;

namespace Auth.Application.Auth;

public sealed class AuthService
{
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenIssuer _tokens;
    private readonly IClock _clock;

    public AuthService(
        IUserRepository users,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        ITokenIssuer tokens,
        IClock clock)
    {
        _users = users;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _tokens = tokens;
        _clock = clock;
    }

    public async Task<User> RegisterAsync(
        string email,
        string password,
        string? displayName,
        CancellationToken cancellationToken)
    {
        Email normalized = Email.Of(email);

        if (await _users.ExistsWithEmailAsync(normalized, cancellationToken))
        {
            throw new EmailAlreadyInUseException(normalized.Value);
        }

        string passwordHash = _passwordHasher.Hash(password);
        User user = User.Register(normalized, passwordHash, displayName, _clock.Now);

        _users.Add(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return user;
    }

    public async Task<IssuedToken> LoginAsync(string email, string password, CancellationToken cancellationToken)
    {
        Email normalized = Email.Of(email);
        User? user = await _users.FindByEmailAsync(normalized, cancellationToken);

        if (user is null || !_passwordHasher.Verify(password, user.PasswordHash))
        {
            throw new InvalidCredentialsException();
        }

        return _tokens.Issue(user);
    }

    public async Task<User> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _users.FindAsync(userId, cancellationToken)
            ?? throw new UserNotFoundException(userId);
    }
}
