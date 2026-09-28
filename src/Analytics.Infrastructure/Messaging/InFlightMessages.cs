namespace Analytics.Infrastructure.Messaging;

// Zählt die Nachrichten, die gerade verarbeitet werden. Beim Herunterfahren darf der Kanal erst
// zu, wenn keine mehr läuft: sonst geht die Bestätigung verloren, der Broker stellt neu zu, und
// die Buchung wird ein zweites Mal verarbeitet. Die Deduplizierung fängt das ab, aber es ist
// Arbeit, die sich vermeiden lässt.
public sealed class InFlightMessages
{
    private readonly object _lock = new object();
    private int _count;
    private TaskCompletionSource _idle = CompletedIdle();

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _count;
            }
        }
    }

    public IDisposable Begin()
    {
        lock (_lock)
        {
            if (_count == 0)
            {
                _idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            _count++;
        }

        return new Work(this);
    }

    public Task WaitUntilIdleAsync(CancellationToken cancellationToken)
    {
        Task idle;
        lock (_lock)
        {
            idle = _idle.Task;
        }

        return idle.WaitAsync(cancellationToken);
    }

    private void End()
    {
        lock (_lock)
        {
            _count--;
            if (_count == 0)
            {
                _idle.TrySetResult();
            }
        }
    }

    private static TaskCompletionSource CompletedIdle()
    {
        TaskCompletionSource idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        idle.SetResult();
        return idle;
    }

    private sealed class Work : IDisposable
    {
        private InFlightMessages? _owner;

        public Work(InFlightMessages owner)
        {
            _owner = owner;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _owner, null)?.End();
        }
    }
}
