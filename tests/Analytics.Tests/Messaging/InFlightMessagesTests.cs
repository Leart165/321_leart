using Analytics.Infrastructure.Messaging;
using Xunit;

namespace Analytics.Tests.Messaging;

// Beim Herunterfahren schliesst der Kanal erst, wenn keine Nachricht mehr in Arbeit ist.
public sealed class InFlightMessagesTests
{
    [Fact]
    public async Task Without_work_it_is_idle_at_once()
    {
        InFlightMessages inFlight = new InFlightMessages();

        await inFlight.WaitUntilIdleAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Equal(0, inFlight.Count);
    }

    [Fact]
    public async Task It_waits_until_the_last_message_is_done()
    {
        InFlightMessages inFlight = new InFlightMessages();
        IDisposable first = inFlight.Begin();
        IDisposable second = inFlight.Begin();

        Task idle = inFlight.WaitUntilIdleAsync(CancellationToken.None);
        first.Dispose();
        await Task.Delay(50);
        Assert.False(idle.IsCompleted);

        second.Dispose();
        await idle.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Equal(0, inFlight.Count);
    }

    [Fact]
    public void Disposing_twice_counts_once()
    {
        InFlightMessages inFlight = new InFlightMessages();
        IDisposable work = inFlight.Begin();
        inFlight.Begin();

        work.Dispose();
        work.Dispose();

        Assert.Equal(1, inFlight.Count);
    }

    [Fact]
    public async Task The_deadline_of_the_host_ends_the_wait()
    {
        InFlightMessages inFlight = new InFlightMessages();
        inFlight.Begin();
        using CancellationTokenSource deadline = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => inFlight.WaitUntilIdleAsync(deadline.Token));
    }
}
