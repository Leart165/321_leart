using Auth.Application.Abstractions;

namespace Auth.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTimeOffset Now
    {
        get { return DateTimeOffset.UtcNow; }
    }
}
