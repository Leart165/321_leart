using Auth.Domain.Users;

namespace Auth.Api.Dtos;

public sealed record UserDto(string Id, string Email, string? DisplayName, DateTimeOffset RegisteredAt)
{
    public static UserDto From(User user)
    {
        return new UserDto(user.Id.ToString(), user.Email.Value, user.DisplayName, user.RegisteredAt);
    }
}
