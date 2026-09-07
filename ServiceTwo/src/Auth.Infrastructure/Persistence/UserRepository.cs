using Auth.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Auth.Infrastructure.Persistence;

public sealed class UserRepository : IUserRepository
{
    private readonly AuthDbContext _context;

    public UserRepository(AuthDbContext context)
    {
        _context = context;
    }

    public Task<User?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        return _context.Users.SingleOrDefaultAsync(user => user.Id == id, cancellationToken);
    }

    public Task<User?> FindByEmailAsync(Email email, CancellationToken cancellationToken)
    {
        return _context.Users.SingleOrDefaultAsync(user => user.Email == email, cancellationToken);
    }

    public Task<bool> ExistsWithEmailAsync(Email email, CancellationToken cancellationToken)
    {
        return _context.Users.AnyAsync(user => user.Email == email, cancellationToken);
    }

    public void Add(User user)
    {
        _context.Users.Add(user);
    }
}
