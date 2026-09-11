namespace Auth.Domain.Users;

public interface IUserRepository
{
    Task<User?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task<User?> FindByEmailAsync(Email email, CancellationToken cancellationToken);

    Task<bool> ExistsWithEmailAsync(Email email, CancellationToken cancellationToken);

    void Add(User user);
}
