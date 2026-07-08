namespace AutoCheck.Services;

/// <summary>
/// Serializes auto-grading runs: one git clone + Gemini call at a time.
/// Prevents parallel clones and 429s when many students submit at once.
/// Registered as a singleton.
/// </summary>
public class GradingQueueService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private int _inQueue;   // running + waiting

    /// <summary>How many gradings are running or waiting right now.</summary>
    public int InQueue => _inQueue;

    /// <summary>Waits for our turn. Dispose the returned handle to release the slot.</summary>
    public async Task<IDisposable> EnterAsync(IProgress<string>? progress, CancellationToken ct)
    {
        var pos = Interlocked.Increment(ref _inQueue);
        try
        {
            if (pos > 1)
                progress?.Report($"У черзі на перевірку — попереду {pos - 1}…");
            await _gate.WaitAsync(ct);
            return new Releaser(this);
        }
        catch
        {
            Interlocked.Decrement(ref _inQueue);
            throw;
        }
    }

    private void Release()
    {
        Interlocked.Decrement(ref _inQueue);
        _gate.Release();
    }

    private sealed class Releaser(GradingQueueService q) : IDisposable
    {
        private int _disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0) q.Release();
        }
    }
}
